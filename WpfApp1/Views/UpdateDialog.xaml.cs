using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Services;

namespace WpfApp1.Views;

public enum UpdateAction
{
    Later,
    Download,
    Skip
}

/// <summary>更新提示窗口。第 1 期只负责展示与跳转下载，不下载、不替换任何文件。</summary>
public partial class UpdateDialog : Window
{
    private readonly UpdateCheckResult _result;

    public UpdateAction Result { get; private set; } = UpdateAction.Later;

    public UpdateDialog(UpdateCheckResult result)
    {
        InitializeComponent();
        _result = result;

        var manifest = result.Manifest;
        TxtTitle.Text = $"发现新版本 v{manifest?.Version ?? "未知"}";

        var meta = new List<string> { $"当前版本 {VersionInfo.DisplayVersion}" };
        if (!string.IsNullOrWhiteSpace(manifest?.ReleaseDate)) meta.Add(manifest.ReleaseDate);
        if (!string.IsNullOrWhiteSpace(manifest?.Channel)) meta.Add($"{manifest.Channel} 通道");
        TxtSubTitle.Text = string.Join(" · ", meta);

        NotesList.ItemsSource = manifest?.Notes ?? new[] { "本次更新内容请查看发布页面。" };
        BtnSkip.Visibility = result.IsMandatory ? Visibility.Collapsed : Visibility.Visible;

        Loaded += (_, _) => BtnDownload.Focus();
    }

    /// <summary>供首页与设置页共用的手动检查入口：检查结果统一在这里给出明确反馈。</summary>
    public static async Task CheckAndShowAsync(bool manual)
    {
        var check = await UpdateService.CheckAsync(manual);

        switch (check.Status)
        {
            case UpdateCheckStatus.Available:
                var owner = GetDialogOwner();
                var dialog = new UpdateDialog(check) { Owner = owner };
                if (owner == null) dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.ShowDialog();
                break;

            case UpdateCheckStatus.UpToDate:
                if (manual) AppMessageBox.Show(check.Message, "检查更新");
                break;

            case UpdateCheckStatus.Failed:
                if (manual)
                {
                    AppMessageBox.Show(
                        $"{check.Message}\n\n可稍后重试，或在「系统参数配置 → 软件更新」中点「选择本地清单」改用离线清单。",
                        "检查更新");
                }
                break;

            case UpdateCheckStatus.Disabled:
                if (manual) AppMessageBox.Show("自动检查已关闭，可在「系统参数配置 → 软件更新」中开启。", "检查更新");
                break;
        }
    }

    private static Window? GetDialogOwner()
    {
        var app = Application.Current;
        if (app == null) return null;
        return app.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive && window.IsVisible)
               ?? (app.MainWindow?.IsVisible == true ? app.MainWindow : null);
    }

    private void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        Result = UpdateAction.Download;
        var url = UpdateService.ResolveDownloadUrl(_result.Manifest);
        Close();
        OpenUrl(url);
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        var settings = UpdateSettingsService.Load();
        settings.SkippedVersion = _result.Manifest?.Version;
        UpdateSettingsService.Save(settings);
        UpdateLogger.Info($"跳过版本 v{settings.SkippedVersion}");
        Result = UpdateAction.Skip;
        Close();
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e)
    {
        Result = UpdateAction.Later;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Result = UpdateAction.Later;
        Close();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UpdateLogger.Warn($"打开下载页面失败：{ex.Message}");
            AppMessageBox.Show($"无法打开下载页面，请手动访问：\n{url}", "检查更新");
        }
    }
}
