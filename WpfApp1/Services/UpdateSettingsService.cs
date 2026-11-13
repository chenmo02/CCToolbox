using System.IO;
using System.Text;
using System.Text.Json;

namespace WpfApp1.Services;

/// <summary>
/// 更新设置的独立持久化。刻意不并入 ImportSettings：设置页保存时会全量覆盖 settings.json，
/// 且 ImportSettingsService.Clone() 是逐字段手写拷贝，新增字段会被静默丢弃。
/// </summary>
public static class UpdateSettingsService
{
    private static readonly object Sync = new();

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CCToolbox");

    public static string ConfigPath => Path.Combine(DataDirectory, "update-settings.json");

    public static UpdateSettings Load()
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    return new UpdateSettings();
                }

                var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                return Normalize(JsonSerializer.Deserialize<UpdateSettings>(json, UpdateJson.Options)
                                 ?? new UpdateSettings());
            }
            catch (Exception ex)
            {
                UpdateLogger.Warn($"读取更新设置失败，已使用默认值：{ex.Message}");
                return new UpdateSettings();
            }
        }
    }

    public static void Save(UpdateSettings settings)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                var json = JsonSerializer.Serialize(Normalize(settings), UpdateJson.Options);
                File.WriteAllText(ConfigPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                UpdateLogger.Warn($"保存更新设置失败：{ex.Message}");
            }
        }
    }

    public static UpdateSettings Normalize(UpdateSettings settings)
    {
        settings.ManifestUrl = string.IsNullOrWhiteSpace(settings.ManifestUrl)
            ? VersionInfo.DefaultManifestUrl
            : settings.ManifestUrl.Trim().Trim('"');
        settings.Channel = string.IsNullOrWhiteSpace(settings.Channel)
            ? VersionInfo.StableChannel
            : settings.Channel.Trim();
        return settings;
    }
}
