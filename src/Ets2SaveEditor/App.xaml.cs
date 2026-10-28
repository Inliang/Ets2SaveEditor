using System;
using System.IO;
using System.Windows;

namespace Ets2SaveEditor
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                LogCrash(e.Exception);
                e.Handled = true;
                if (Environment.UserInteractive && !LogOnly())
                {
                    MessageBox.Show("发生未处理异常：" + InnerMessage(e.Exception), "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                }
                else
                {
                    Environment.Exit(1);
                }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogCrash(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
                if (!Environment.UserInteractive || LogOnly()) Environment.Exit(1);
            };
        }

        private static bool LogOnly() =>
            string.Equals(Environment.GetEnvironmentVariable("ETS2_LOGCRASH"), "1", StringComparison.Ordinal);

        private static void LogCrash(Exception ex)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.WriteAllText(path, ex?.ToString() ?? "null");
            }
            catch { }
        }

        private static string? InnerMessage(Exception? ex)
        {
            var m = ex?.Message;
            while (ex?.InnerException != null) { ex = ex.InnerException; m = ex.Message; }
            return m;
        }
    }
}
