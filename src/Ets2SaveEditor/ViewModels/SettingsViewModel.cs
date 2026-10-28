using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ets2SaveEditor.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] private bool _autoBackup = true;
        [ObservableProperty] private bool _steamDetection = true;
        [ObservableProperty] private string _defaultSavePath = "";

        private static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Ets2SaveEditor", "settings.json");

        public void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var s = JsonSerializer.Deserialize<SettingsViewModel>(json);
                    if (s != null)
                    {
                        AutoBackup = s.AutoBackup;
                        SteamDetection = s.SteamDetection;
                        DefaultSavePath = s.DefaultSavePath ?? "";
                    }
                }
            }
            catch { /* 忽略损坏的设置文件 */ }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { /* 忽略保存失败 */ }
        }
    }
}
