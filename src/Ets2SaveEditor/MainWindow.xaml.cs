using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ets2SaveEditor.Core.Persistence;
using Ets2SaveEditor.ViewModels;

namespace Ets2SaveEditor
{
    public partial class MainWindow : Window
    {
        public MainViewModel VM => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            VM.RefreshSlotsCommand.Execute(null);
        }

        private void MiOpenFile_Click(object sender, RoutedEventArgs e) => VM.OpenFileCommand.Execute(null);
        private void MiOpenFolder_Click(object sender, RoutedEventArgs e) => VM.OpenFolderCommand.Execute(null);
        private void MiSave_Click(object sender, RoutedEventArgs e) => VM.SaveCommand.Execute(null);
        private void MiSaveAs_Click(object sender, RoutedEventArgs e) => VM.SaveAsCommand.Execute(null);
        private void MiExit_Click(object sender, RoutedEventArgs e) => VM.ExitCommand.Execute(null);
        private void MiAbout_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Ets2SaveEditor 是一个开源的欧卡2（ETS2）存档修改器。\n\n" +
                            "⚠ 修改游戏存档存在风险，请务必在编辑前备份！\n" +
                            "本工具按“原样”提供，作者不对任何存档损坏或账号封禁负责。\n" +
                            "SII 解析采用无损透传，未识别字段原样保留。", "关于 / 免责声明");

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => VM.RefreshSlotsCommand.Execute(null);

        private void BtnLoadSave_Click(object sender, RoutedEventArgs e) => VM.OpenFileCommand.Execute(null);

        private void BtnLoadSelected_Click(object sender, RoutedEventArgs e)
        {
            if (LbFiles.SelectedItem is string file)
                VM.LoadFile(file);
            else
                MessageBox.Show("请先在下方列表中选择一个 .sii 文件，再点击「加载选中文件」。", "提示");
        }

        private void LbSlots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbSlots.SelectedItem is SaveSlot slot)
            {
                LbFiles.ItemsSource = new SaveLocationFinder().ListSiiFiles(slot.FullPath);
            }
        }

        private void LbFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LbFiles.SelectedItem is string file)
                VM.LoadFile(file);
        }
    }
}
