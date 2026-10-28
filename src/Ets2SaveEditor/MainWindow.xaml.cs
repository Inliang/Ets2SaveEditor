using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private void BtnBrowseFile_Click(object sender, RoutedEventArgs e) => VM.OpenFileCommand.Execute(null);
        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e) => VM.OpenSlotFolderCommand.Execute(null);
        private void BtnLoadSelected_Click(object sender, RoutedEventArgs e) => VM.LoadSelectedSlotCommand.Execute(null);

        /// <summary>切换游戏（欧卡 2 / 美卡 1）后重新筛选档案列表。</summary>
        private void RbGame_Checked(object sender, RoutedEventArgs e)
        {
            // IsChecked="True" 会在 InitializeComponent 期间触发本事件，
            // 此时 DataContext 尚未赋值，必须先判空。
            if (DataContext is not MainViewModel vm) return;
            if (sender is RadioButton rb && rb.Tag is string game && rb.IsChecked == true)
            {
                vm.SelectedGame = game;
            }
        }

        private void LbSlotFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LbSlotFiles.SelectedItem is SlotFileItem file)
            {
                VM.LoadFile(file.FullPath);
            }
        }
    }
}
