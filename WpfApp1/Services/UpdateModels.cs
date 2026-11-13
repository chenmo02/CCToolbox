using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfApp1.Services;

/// <summary>更新清单（latest.json）的数据契约。第 2 期的包校验字段在此预留，第 1 期不读取。</summary>
public sealed class UpdateManifest
{
    public int Schema { get; set; }
    public string Product { get; set; } = string.Empty;
    public string Channel { get; set; } = VersionInfo.StableChannel;
    public string Version { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string? MinimumVersion { get; set; }
    public bool Mandatory { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ReleaseNotesUrl { get; set; }
    private List<string> _changelog = new();

    public List<string> Changelog
    {
        get => _changelog;
        set => _changelog = value ?? new List<string>();
    }

    public UpdatePackage? Package { get; set; }

    [JsonIgnore]
    public Version ParsedVersion => VersionInfo.Parse(Version);

    [JsonIgnore]
    public IReadOnlyList<string> Notes => _changelog.Count > 0
        ? _changelog
        : new[] { "本次更新内容请查看发布页面。" };
}

/// <summary>安装包信息。size / sha256 由第 2 期下载校验使用，第 1 期允许缺省。</summary>
public sealed class UpdatePackage
{
    public string? Url { get; set; }
    public long Size { get; set; }
    public string? Sha256 { get; set; }
    public string? Rid { get; set; }
}

/// <summary>更新相关用户设置，独立于 settings.json 存储。</summary>
public sealed class UpdateSettings
{
    public bool AutoCheck { get; set; } = true;
    public string Channel { get; set; } = VersionInfo.StableChannel;
    public string ManifestUrl { get; set; } = VersionInfo.DefaultManifestUrl;
    public string? SkippedVersion { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
}

public enum UpdateCheckStatus
{
    UpToDate,
    Available,
    Failed,
    Disabled
}

public sealed class UpdateCheckResult
{
    public UpdateCheckStatus Status { get; set; }
    public UpdateManifest? Manifest { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsMandatory => Manifest?.Mandatory == true;
}

internal static class UpdateJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
