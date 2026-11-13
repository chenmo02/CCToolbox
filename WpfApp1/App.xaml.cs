using System.Windows;
using System.Windows.Threading;
using WpfApp1.Services;

namespace CCToolbox
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 捕获 UI 线程未处理异常
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 捕获非 UI 线程未处理异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 捕获 Task 中未观察到的异常
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // 启动后延迟静默检查更新：失败一律写日志，不弹窗、不阻塞主窗口
            _ = RunSilentUpdateCheckAsync();
        }

        private static async Task RunSilentUpdateCheckAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(4)).ConfigureAwait(false);
                await UpdateService.CheckAsync(manual: false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 双保险：UpdateService 内部已收敛异常，这里防止任何遗漏变成未观察任务异常
                UpdateLogger.Warn($"启动检查更新异常：{ex.Message}");
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"发生未处理的异常：\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // 阻止闪退
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"发生严重异常：\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            MessageBox.Show(
                $"后台任务异常：\n\n{e.Exception.InnerException?.Message ?? e.Exception.Message}",
                "程序异常", MessageBoxButton.OK, MessageBoxImage.Error);
            e.SetObserved(); // 标记已处理
        }
    }
}
