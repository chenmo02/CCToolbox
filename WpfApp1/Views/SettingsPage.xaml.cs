using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();

            LoadSettings();

            // 检查结果统一在设置页呈现：按项目约定在 Loaded 订阅、Unloaded 退订静态事件
            Loaded += SettingsPage_Loaded;
            Unloaded += SettingsPage_Unloaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateService.CheckCompleted += OnUpdateCheckCompleted;
            RefreshUpdateStatus();
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            UpdateService.CheckCompleted -= OnUpdateCheckCompleted;
        }

        private void OnUpdateCheckCompleted(object? sender, UpdateCheckResult result)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsLoaded) RefreshUpdateStatus();
            }));
        }

        // ═══════════════════════════════════════
        // 设置持久化
        // ═══════════════════════════════════════

        private void LoadSettings()
        {
            ImportSettings settings = ImportSettingsService.Load();
            SelectDbType(settings.DefaultDbType);
            TxtDefaultTableName.Text = settings.DefaultTableName;
            TxtDefaultBatchSize.Text = settings.BatchSize.ToString(CultureInfo.InvariantCulture);
            ChkDefaultDropIfExists.IsChecked = settings.DropIfExists;
            ChkDefaultBatchInsert.IsChecked = settings.BatchInsert;
            ChkDefaultLimitFieldLength.IsChecked = settings.LimitFieldLength;
            TxtDefaultExportPath.Text = settings.DefaultExportPath;

            LoadUpdateSettings();
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(TxtDefaultBatchSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int batchSize) || batchSize <= 0)
                {
                    MessageBox.Show("每批 INSERT 行数必须是大于 0 的整数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtDefaultBatchSize.Focus();
                    return;
                }

                string exportPath = TxtDefaultExportPath.Text.Trim();
                if (!string.IsNullOrWhiteSpace(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }

                var settings = new ImportSettings
                {
                    QuickTablePrefix = ImportSettingsService.Load().QuickTablePrefix,
                    DefaultDbType = GetSelectedDbType(),
                    DefaultTableName = TxtDefaultTableName.Text.Trim(),
                    BatchSize = batchSize,
                    DropIfExists = ChkDefaultDropIfExists.IsChecked == true,
                    BatchInsert = ChkDefaultBatchInsert.IsChecked == true,
                    LimitFieldLength = ChkDefaultLimitFieldLength.IsChecked == true,
                    DefaultExportPath = exportPath
                };

                settings = ImportSettingsService.Normalize(settings);
                ImportSettingsService.Save(settings);
                TxtDefaultTableName.Text = settings.DefaultTableName;

                ToastService.Show(this, "✅ 设置已保存");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSelectExportPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择默认导出路径" };
            if (dialog.ShowDialog() == true)
                TxtDefaultExportPath.Text = dialog.FolderName;
        }

        private void BtnOpenCsvCompare_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
                mainWindow.NavigateToCsvCompare();
        }

        private void SelectDbType(string dbType)
        {
            foreach (ComboBoxItem item in CmbDefaultDbType.Items)
            {
                string content = item.Content?.ToString() ?? string.Empty;
                if (string.Equals(content, dbType, StringComparison.OrdinalIgnoreCase))
                {
                    CmbDefaultDbType.SelectedItem = item;
                    return;
                }
            }

            CmbDefaultDbType.SelectedIndex = 0;
        }

        private string GetSelectedDbType() => (CmbDefaultDbType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PostgreSQL";

        // ═══════════════════════════════════════
        // 软件更新设置（独立配置文件，不并入 ImportSettings）
        // ═══════════════════════════════════════

        private void LoadUpdateSettings()
        {
            UpdateSettings settings = UpdateSettingsService.Load();
            TglAutoUpdateCheck.IsChecked = settings.AutoCheck;
            TxtUpdateManifestUrl.Text = settings.ManifestUrl;
            SelectUpdateChannel(settings.Channel);
            RefreshUpdateStatus();
        }

        private void RefreshUpdateStatus()
        {
            var result = UpdateService.LastResult;
            if (result?.Status == UpdateCheckStatus.Available)
            {
                TxtLastUpdateCheck.Text = $"发现新版本 v{result.Manifest!.Version}，点击「立即检查」查看详情";
                TxtLastUpdateCheck.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2410C"));
                return;
            }

            var settings = UpdateSettingsService.Load();
            TxtLastUpdateCheck.Text = settings.LastCheckedAt.HasValue
                ? $"上次检查：{settings.LastCheckedAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                : "尚未检查过更新";
            TxtLastUpdateCheck.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
        }

        private void SelectUpdateChannel(string channel)
        {
            foreach (ComboBoxItem item in CmbUpdateChannel.Items)
            {
                if (string.Equals(item.Content?.ToString(), channel, StringComparison.OrdinalIgnoreCase))
                {
                    CmbUpdateChannel.SelectedItem = item;
                    return;
                }
            }

            CmbUpdateChannel.SelectedIndex = 0;
        }

        private void BtnSaveUpdateSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateSettings settings = UpdateSettingsService.Load();
                settings.AutoCheck = TglAutoUpdateCheck.IsChecked == true;
                settings.Channel = (CmbUpdateChannel.SelectedItem as ComboBoxItem)?.Content?.ToString()
                                   ?? VersionInfo.StableChannel;
                settings.ManifestUrl = TxtUpdateManifestUrl.Text.Trim();
                UpdateSettingsService.Save(settings);
                LoadUpdateSettings();
                ToastService.Show(this, "✅ 更新设置已保存");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存更新设置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnCheckUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdateNow.IsEnabled = false;
            try
            {
                await UpdateDialog.CheckAndShowAsync(manual: true);
            }
            finally
            {
                BtnCheckUpdateNow.IsEnabled = true;
                RefreshUpdateStatus();
            }
        }

        private void BtnResetUpdateSource_Click(object sender, RoutedEventArgs e)
        {
            TxtUpdateManifestUrl.Text = VersionInfo.DefaultManifestUrl;
            ToastService.Show(this, "已恢复默认更新源，请点击保存");
        }

        private void BtnPickLocalManifest_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择更新清单 latest.json",
                Filter = "更新清单 (*.json)|*.json|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                TxtUpdateManifestUrl.Text = dialog.FileName;
                ToastService.Show(this, "已填入本地清单路径，请点击保存");
            }
        }

        private void BtnOpenUpdateConfigDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(UpdateSettingsService.DataDirectory);
                Process.Start(new ProcessStartInfo("explorer.exe", UpdateSettingsService.DataDirectory)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开配置目录：{ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
