using System.Windows;

namespace Ets2SaveEditor
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                System.Windows.MessageBox.Show("发生未处理异常：" + e.Exception.Message, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
        }
    }
}
