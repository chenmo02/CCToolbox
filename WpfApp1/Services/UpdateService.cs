using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WpfApp1.Services;

/// <summary>
/// 第 1 期更新服务：读取更新清单 → 版本比较 → 给出结果。不下载、不替换、不改本地文件。
/// 所有异常在本类型内部收敛并写日志，绝不把未观察异常抛给 App.TaskScheduler_UnobservedTaskException。
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient HttpClient = CreateClient();
    private static readonly SemaphoreSlim CheckGate = new(1, 1);
    private static readonly object StateSync = new();
    private static UpdateCheckResult? _lastResult;

    /// <summary>检查完成通知。事件可能在后台线程触发，订阅方须自行封送到 UI 线程。</summary>
    public static event EventHandler<UpdateCheckResult>? CheckCompleted;

    public static UpdateCheckResult? LastResult
    {
        get
        {
            lock (StateSync) return _lastResult;
        }
    }

    public static async Task<UpdateCheckResult> CheckAsync(bool manual, CancellationToken cancellationToken = default)
    {
        if (!await CheckGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.Failed,
                Message = "正在检查更新，请稍候。"
            };
        }

        try
        {
            var settings = UpdateSettingsService.Load();
            if (!manual && !settings.AutoCheck)
            {
                return Finish(new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.Disabled,
                    Message = "自动检查更新已关闭。"
                }, notify: false);
            }

            var json = await FetchWithFallbackAsync(settings.ManifestUrl, cancellationToken).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, UpdateJson.Options)
                           ?? throw new InvalidDataException("更新清单为空。");
            var result = Evaluate(manifest, settings);

            settings.LastCheckedAt = DateTimeOffset.Now;
            UpdateSettingsService.Save(settings);
            UpdateLogger.Info($"检查更新：{result.Status} · {result.Message}");
            return Finish(result, notify: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var result = Failed("连接更新源超时（10 秒）：网络不可达或站点被拦截。可稍后重试，或改用本地 / UNC 清单。");
            UpdateLogger.Warn(result.Message);
            return Finish(result, notify: true);
        }
        catch (OperationCanceledException)
        {
            return Finish(Failed("检查已取消。"), notify: false);
        }
        catch (Exception ex)
        {
            var result = Failed(Describe(ex));
            UpdateLogger.Warn($"检查更新失败：{ex.GetType().Name} · {ex.Message}");
            return Finish(result, notify: true);
        }
        finally
        {
            CheckGate.Release();
        }
    }

    /// <summary>解析下载页地址。非 HTTPS 或非 GitHub 官方域一律回退到 Releases 页，避免清单被篡改后指向钓鱼站。</summary>
    public static string ResolveDownloadUrl(UpdateManifest? manifest)
    {
        var raw = manifest?.DownloadUrl;
        if (string.IsNullOrWhiteSpace(raw)) return VersionInfo.DefaultDownloadUrl;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return VersionInfo.DefaultDownloadUrl;
        }

        var host = uri.Host.ToLowerInvariant();
        var trusted = host == "github.com"
                      || host.EndsWith(".github.com", StringComparison.Ordinal)
                      || host == "github.io"
                      || host.EndsWith(".github.io", StringComparison.Ordinal);
        return trusted ? raw.Trim() : VersionInfo.DefaultDownloadUrl;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"CCToolbox/{VersionInfo.DisplayVersion}");
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return client;
    }

    /// <summary>
    /// 默认更新源失败时自动回退到同仓库的 raw 清单。仅对默认地址生效，
    /// 用户自定义的更新源（本地路径 / UNC / 内网 HTTP）一律不擅自转向公网。
    /// </summary>
    private static async Task<string> FetchWithFallbackAsync(string source, CancellationToken cancellationToken)
    {
        if (!IsDefaultManifestUrl(source))
        {
            return await FetchManifestAsync(source, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            // 主源最多等 4 秒：被代理拦截或站点不可达时尽快切到备用源，避免手动检查久等
            using var primaryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            primaryCts.CancelAfter(TimeSpan.FromSeconds(4));
            return await FetchManifestAsync(source, primaryCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            UpdateLogger.Warn($"主更新源不可用（{ex.Message}），改用同仓库 raw 备用清单");
            var json = await FetchManifestAsync(VersionInfo.FallbackManifestUrl, cancellationToken).ConfigureAwait(false);
            UpdateLogger.Info("已通过备用更新源获取清单");
            return json;
        }
    }

    private static bool IsDefaultManifestUrl(string source) =>
        string.Equals(source.Trim().TrimEnd('/'), VersionInfo.DefaultManifestUrl, StringComparison.OrdinalIgnoreCase);

    private static async Task<string> FetchManifestAsync(string source, CancellationToken cancellationToken)
    {
        var target = (source ?? string.Empty).Trim();
        if (target.Length == 0) throw new InvalidDataException("更新源地址为空。");

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            // 加时间戳参数，避免 CDN / 代理返回旧清单
            var separator = target.Contains('?') ? "&" : "?";
            var url = $"{target}{separator}_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            using var response = await HttpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // 保留状态码，便于把 404 / 503 / 网络不通区分成不同的用户提示
                throw new HttpRequestException(
                    $"更新源返回 HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    null,
                    response.StatusCode);
            }

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        // 本地路径 / UNC 共享 / U 盘离线包：按文件读取，不走 HTTP
        var path = target.Trim('"');
        if (!File.Exists(path)) throw new FileNotFoundException("更新清单文件不存在。", path);
        return await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static UpdateCheckResult Evaluate(UpdateManifest manifest, UpdateSettings settings)
    {
        if (manifest.Schema != 1)
            return Failed($"更新清单格式版本 {manifest.Schema} 不受支持。");
        if (!string.Equals(manifest.Product, VersionInfo.ProductId, StringComparison.OrdinalIgnoreCase))
            return Failed("更新清单不是本产品的清单。");

        var server = manifest.ParsedVersion;
        if (server.Major == 0 && server.Minor == 0 && server.Build == 0)
            return Failed("更新清单中的版本号无效。");

        if (!string.IsNullOrWhiteSpace(manifest.ReleaseDate) &&
            DateTime.TryParse(manifest.ReleaseDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var releaseDate) &&
            releaseDate.Date > DateTime.Today.AddDays(1))
        {
            return Failed("更新清单的发布日期异常，已忽略本次结果。");
        }

        var current = VersionInfo.CurrentVersion;
        if (server <= current)
        {
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.UpToDate,
                Manifest = manifest,
                Message = $"当前 {VersionInfo.DisplayVersion} 已是最新版本。"
            };
        }

        if (!manifest.Mandatory &&
            string.Equals(settings.SkippedVersion, manifest.Version, StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.UpToDate,
                Manifest = manifest,
                Message = $"已跳过 v{manifest.Version}。"
            };
        }

        if (!string.IsNullOrWhiteSpace(manifest.MinimumVersion))
        {
            var minimum = VersionInfo.Parse(manifest.MinimumVersion);
            if (current < minimum) manifest.Mandatory = true;
        }

        return new UpdateCheckResult
        {
            Status = UpdateCheckStatus.Available,
            Manifest = manifest,
            Message = $"发现新版本 v{manifest.Version}。"
        };
    }

    private static UpdateCheckResult Failed(string message) =>
        new() { Status = UpdateCheckStatus.Failed, Message = message };

    private static UpdateCheckResult Finish(UpdateCheckResult result, bool notify)
    {
        lock (StateSync) _lastResult = result;
        if (notify) CheckCompleted?.Invoke(null, result);
        return result;
    }

    private static string Describe(Exception ex) => ex switch
    {
        HttpRequestException http when http.StatusCode == HttpStatusCode.NotFound =>
            "更新源返回 404：清单尚未发布或地址不正确。请确认已推送到 GitHub 仓库，或改用本地 / UNC 清单。",
        HttpRequestException http when http.StatusCode == HttpStatusCode.ServiceUnavailable =>
            "更新源返回 503：服务暂时不可用（常见于网络代理、限流或站点被拦截）。可稍后重试，或改用本地 / UNC 清单。",
        HttpRequestException http when http.StatusCode.HasValue && (int)http.StatusCode.Value >= 500 =>
            $"更新源服务端错误（HTTP {(int)http.StatusCode.Value}），请稍后重试或改用本地 / UNC 清单。",
        HttpRequestException http when http.StatusCode.HasValue =>
            $"更新源返回 HTTP {(int)http.StatusCode.Value}，请检查更新源地址。",
        HttpRequestException => "无法连接更新源：网络不可达、DNS 解析失败或被代理拦截。可改用本地 / UNC 清单。",
        JsonException => "更新清单格式不正确。",
        FileNotFoundException => "更新清单文件不存在，请检查更新源路径。",
        UnauthorizedAccessException => "没有读取更新清单的权限。",
        IOException => "读取更新清单时发生 I/O 错误。",
        _ => $"检查更新失败：{ex.Message}"
    };
}
