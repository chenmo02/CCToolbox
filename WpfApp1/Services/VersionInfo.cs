using System.Reflection;

namespace WpfApp1.Services;

/// <summary>
/// 版本信息的唯一真源。所有版本号一律从程序集元数据读取，界面与更新比较不得写死字符串。
/// </summary>
public static class VersionInfo
{
    public const string ProductId = "CCToolbox";
    public const string StableChannel = "stable";
    public const string DefaultManifestUrl = "https://chenmo02.github.io/CCToolbox/updates/stable/latest.json";
    /// <summary>同仓库的备用清单源：Pages 被代理/缓存拦截时自动回退，仅对默认更新源生效。</summary>
    public const string FallbackManifestUrl = "https://raw.githubusercontent.com/chenmo02/CCToolbox/main/updates/stable/latest.json";
    public const string DefaultDownloadUrl = "https://github.com/chenmo02/CCToolbox/releases/latest";

    static VersionInfo()
    {
        CurrentVersion = ResolveVersion();
        DisplayVersion = $"v{CurrentVersion.ToString(3)}";
        ReleaseDate = ResolveReleaseDate();
    }

    /// <summary>当前程序版本，统一规整为三段（主.次.修订），忽略第四段 Revision。</summary>
    public static Version CurrentVersion { get; }

    /// <summary>界面展示用版本号，例如 v2.3.8。</summary>
    public static string DisplayVersion { get; }

    /// <summary>编译时写入的发布日期，取不到时返回「未知」。</summary>
    public static string ReleaseDate { get; }

    /// <summary>解析清单版本号；无法解析时返回 0.0.0，由调用方判定为无效清单。</summary>
    public static Version Parse(string? value) =>
        Version.TryParse((value ?? string.Empty).Trim(), out var parsed)
            ? Normalize(parsed)
            : new Version(0, 0, 0);

    private static Version ResolveVersion()
    {
        try
        {
            var raw = typeof(VersionInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            // InformationalVersion 可能带 +commit / -preview 后缀，只取语义版本部分
            var core = (raw ?? string.Empty).Split('+', '-')[0].Trim();
            if (Version.TryParse(core, out var parsed))
            {
                return Normalize(parsed);
            }
        }
        catch
        {
            // 回退到程序集名称中的版本
        }

        try
        {
            return Normalize(typeof(VersionInfo).Assembly.GetName().Version ?? new Version(0, 0, 0));
        }
        catch
        {
            return new Version(0, 0, 0);
        }
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));

    private static string ResolveReleaseDate()
    {
        try
        {
            return typeof(VersionInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "ReleaseDate")?.Value ?? "未知";
        }
        catch
        {
            return "未知";
        }
    }
}
