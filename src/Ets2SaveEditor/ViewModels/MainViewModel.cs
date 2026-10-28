using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Persistence;
using Ets2SaveEditor.Services;

namespace Ets2SaveEditor.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DialogService _dialogs = new DialogService();

        [ObservableProperty] private SaveFile? _currentSave;
        [ObservableProperty] private string _statusText = "未加载存档";
        [ObservableProperty] private ObservableCollection<SaveSlot> _saveSlots = new();

        public ProfileViewModel Profile { get; } = new ProfileViewModel();
        public GaragesViewModel Garages { get; } = new GaragesViewModel();
        public TruckViewModel Truck { get; } = new TruckViewModel();
        public TrailerViewModel Trailer { get; } = new TrailerViewModel();
        public FreightMarketViewModel Freight { get; } = new FreightMarketViewModel();
        public CargoViewModel Cargo { get; } = new CargoViewModel();
        public SettingsViewModel Settings { get; } = new SettingsViewModel();

        public MainViewModel()
        {
            Settings.Load();
        }

        [RelayCommand]
        private void RefreshSlots()
        {
            var list = new List<SaveSlot>();
            try { list.AddRange(new SaveLocationFinder().FindLocalProfiles()); } catch { }
            if (Settings.SteamDetection)
            {
                try { list.AddRange(new SaveLocationFinder().FindSteamProfiles()); } catch { }
            }
            SaveSlots = new ObservableCollection<SaveSlot>(list);
            if (list.Count == 0)
                StatusText = "未自动发现存档，请点击左上角「加载存档」手动选择 .sii 文件。";
            else
                StatusText = $"已发现 {list.Count} 个存档槽；双击 .sii 文件或选中后点「加载选中文件」。";
        }

        [RelayCommand]
        private void OpenFile()
        {
            var path = _dialogs.OpenSiiFile();
            if (path != null) LoadFile(path);
        }

        [RelayCommand]
        private void OpenFolder()
        {
            var folder = _dialogs.SelectFolder();
            if (folder == null) return;
            var files = new SaveLocationFinder().ListSiiFiles(folder);
            if (files.Count == 0)
            {
                _dialogs.ShowMessage("该文件夹下没有找到 .sii 文件。");
                return;
            }
            var target = files.Find(f => System.IO.Path.GetFileName(f).Equals("game.sii", StringComparison.OrdinalIgnoreCase))
                         ?? files.Find(f => System.IO.Path.GetFileName(f).Equals("economy.sii", StringComparison.OrdinalIgnoreCase))
                         ?? files[0];
            LoadFile(target);
        }

        public void LoadFile(string path)
        {
            try
            {
                var sf = new SaveFile();
                sf.Load(path);
                CurrentSave = sf;
                Profile.LoadFrom(sf);
                Garages.LoadFrom(sf);
                Truck.LoadFrom(sf);
                Trailer.LoadFrom(sf);
                Freight.LoadFrom(sf);
                Cargo.LoadFrom(sf);
                StatusText = "已加载：" + path;
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("打开失败：" + ex.Message);
                StatusText = "打开失败";
            }
        }

        [RelayCommand]
        private void Save()
        {
            if (CurrentSave == null || !CurrentSave.IsLoaded) { _dialogs.ShowMessage("请先打开一个存档文件。"); return; }
            try
            {
                Profile.ApplyToModel();
                Garages.ApplyToModel();
                Truck.ApplyToModel();
                Trailer.ApplyToModel();
                Freight.ApplyToModel();
                CurrentSave.Save(CurrentSave.Path, Settings.AutoBackup);
                StatusText = "已保存：" + CurrentSave.Path;
                _dialogs.ShowMessage("保存成功。游戏下次加载时会自动重新压缩该存档。");
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("保存失败：" + ex.Message);
            }
        }

        [RelayCommand]
        private void SaveAs()
        {
            if (CurrentSave == null || !CurrentSave.IsLoaded) { _dialogs.ShowMessage("请先打开一个存档文件。"); return; }
            var path = _dialogs.SaveSiiFile(System.IO.Path.GetFileName(CurrentSave.Path));
            if (path == null) return;
            try
            {
                Profile.ApplyToModel();
                Garages.ApplyToModel();
                Truck.ApplyToModel();
                Trailer.ApplyToModel();
                Freight.ApplyToModel();
                CurrentSave.Save(path, Settings.AutoBackup);
                StatusText = "已另存为：" + path;
                _dialogs.ShowMessage("另存为成功。");
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("保存失败：" + ex.Message);
            }
        }

        [RelayCommand]
        private void Exit()
        {
            Application.Current.Shutdown();
        }
    }
}
