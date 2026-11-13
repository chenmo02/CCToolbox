using System.IO;
using System.Text;

namespace WpfApp1.Services;

/// <summary>更新模块独立日志，写入 %LOCALAPPDATA%\CCToolbox\logs\update.log，任何写入失败都被忽略。</summary>
public static class UpdateLogger
{
    private const long MaxLogBytes = 512 * 1024;
    private static readonly object Sync = new();

    public static string LogDirectory => Path.Combine(UpdateSettingsService.DataDirectory, "logs");

    public static string LogPath => Path.Combine(LogDirectory, "update.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);

                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                {
                    var lines = File.ReadAllLines(LogPath, Encoding.UTF8);
                    File.WriteAllLines(LogPath, lines.TakeLast(2000), Encoding.UTF8);
                }

                var safeMessage = (message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
                File.AppendAllText(
                    LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {safeMessage}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch
            {
                // 日志失败不能影响主程序
            }
        }
    }
}
