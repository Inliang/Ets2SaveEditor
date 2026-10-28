using Microsoft.Win32;
using System;
using System.IO;

namespace Ets2SaveEditor.Services
{
    /// <summary>
    /// 封装文件/文件夹选择对话框与消息提示。
    /// </summary>
    public class DialogService
    {
        public string? OpenSiiFile()
        {
            var d = new OpenFileDialog
            {
                Filter = "SII 存档文件 (*.sii)|*.sii|所有文件 (*.*)|*.*",
                Title = "打开 SII 存档文件"
            };
            return d.ShowDialog() == true ? d.FileName : null;
        }

        public string? SaveSiiFile(string defaultName)
        {
            var d = new SaveFileDialog
            {
                Filter = "SII 文件 (*.sii)|*.sii|所有文件 (*.*)|*.*",
                FileName = defaultName ?? "game.sii",
                Title = "保存 SII 文件"
            };
            return d.ShowDialog() == true ? d.FileName : null;
        }

        public string? SelectFolder()
        {
            using var d = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择存档文件夹（包含 .sii 文件的槽位）",
                UseDescriptionForTitle = true
            };
            return d.ShowDialog() == System.Windows.Forms.DialogResult.OK ? d.SelectedPath : null;
        }

        public void ShowMessage(string message, string title = "提示")
        {
            System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        public void ShowError(string message, string title = "错误")
        {
            System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
