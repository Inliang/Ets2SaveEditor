using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Persistence;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Services;

namespace Ets2SaveEditor.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DialogService _dialogs = new DialogService();

        /// <summary>最近一次扫描到的全部存档槽（未按游戏过滤）。</summary>
        private List<SaveSlot> _allSlots = new List<SaveSlot>();

        [ObservableProperty] private SaveFile? _currentSave;
        [ObservableProperty] private string _statusText = "未加载存档";

        /// <summary>当前选择的游戏："ETS2" 或 "ATS"。</summary>
        [ObservableProperty] private string _selectedGame = "ETS2";

        /// <summary>按游戏过滤后的档案列表。</summary>
        [ObservableProperty] private ObservableCollection<SaveProfileItem> _profiles = new();

        /// <summary>当前选中的档案。</summary>
        [ObservableProperty] private SaveProfileItem? _selectedProfile;

        /// <summary>当前档案下的存档槽。</summary>
        [ObservableProperty] private ObservableCollection<SaveSlot> _slots = new();

        /// <summary>当前选中的存档槽。</summary>
        [ObservableProperty] private SaveSlot? _selectedSlot;

        /// <summary>当前存档槽内的 .sii 文件。</summary>
        [ObservableProperty] private ObservableCollection<SlotFileItem> _slotFiles = new();

        /// <summary>已加载文件的文件名（右侧信息区）。</summary>
        [ObservableProperty] private string _loadedFileName = "（尚未加载）";

        /// <summary>已加载存档的概要信息（右侧信息区）。</summary>
        [ObservableProperty] private string _loadedSummary = "请在右侧选择档案与存档槽，然后点击「加载存档」。";

        /// <summary>当前档案的可读名称（未选中时显示「（无）」）。</summary>
        public string ProfileDisplay => SelectedProfile?.FriendlyName ?? "（无）";

        /// <summary>当前存档槽名称（未选中时显示「（无）」）。</summary>
        public string SlotDisplay => SelectedSlot?.SlotName ?? "（无）";

        /// <summary>
        /// 是否已加载存档——工具栏上的「保存」据此禁用，
        /// 避免在没载入存档时误点保存。
        /// </summary>
        public bool HasLoadedSave => CurrentSave != null && CurrentSave.IsLoaded;

        /// <summary>工具栏上显示的当前存档路径（未加载时为空）。</summary>
        public string LoadedPathText => CurrentSave != null && CurrentSave.IsLoaded ? CurrentSave.Path : string.Empty;

        /// <summary>
        /// 工具栏上显示的**存档格式标记**，让用户在保存之前就知道存档会被写成什么格式。
        /// <para>
        /// 本工具保存时统一写明文 SII（游戏能正常读取），而游戏原生的加密容器
        /// <c>ScsC</c> **无法再变回**（HMAC 密钥未公开）。原文件会自动备份成
        /// <c>.bak.&lt;时间戳&gt;</c>，所以明文化**是可逆的、数据不会丢** ——
        /// 但**用户在点保存之前仍然必须知道**存档格式会被改写。
        /// </para>
        /// <para>
        /// 措辞上区分两种状态：「加密 ScsC（原始）」= 这还是游戏原始的加密文件；
        /// 「明文 SII（保存后）」= 这是工具保存过的明文文件。
        /// </para>
        /// <para>未加载存档时为空串（不显示标记）。</para>
        /// </summary>
        public string SaveFormatText
        {
            get
            {
                if (CurrentSave == null || !CurrentSave.IsLoaded) return string.Empty;

                return CurrentSave.Format switch
                {
                    SiiFormat.Plaintext => "存档格式：明文 SII（保存后）",
                    SiiFormat.Encrypted => "存档格式：加密 ScsC（原始）",
                    SiiFormat.Zlib => "存档格式：zlib 压缩（原始）",
                    SiiFormat.Binary => "存档格式：BSII 二进制（原始）",
                    _ => string.Empty,
                };
            }
        }

        /// <summary>格式标记的悬停说明：讲清后果与可逆性。</summary>
        public string SaveFormatTip
        {
            get
            {
                if (CurrentSave == null || !CurrentSave.IsLoaded) return string.Empty;

                if (CurrentSave.Format == SiiFormat.Plaintext)
                {
                    return "此存档当前已是明文 SII 格式（通常是本工具保存的结果），" +
                           "保存后会保持该格式。";
                }

                return "此存档为游戏原生的" + FormatText(CurrentSave.Format) + "格式（原始文件）。" +
                       "本工具保存时统一写为明文 SII（游戏可正常读取），但**无法再变回加密格式**" +
                       "（HMAC 密钥未公开）。原文件会自动备份为 .bak.<时间戳>，可随时还原" +
                       "—— 所以数据不会丢，只是格式变成了明文。";
            }
        }

        public ProfileViewModel Profile { get; } = new ProfileViewModel();
        public GaragesViewModel Garages { get; } = new GaragesViewModel();
        public TruckViewModel Truck { get; } = new TruckViewModel();
        public TrailerViewModel Trailer { get; } = new TrailerViewModel();
        public FreightMarketViewModel Freight { get; } = new FreightMarketViewModel();
        public CargoViewModel Cargo { get; } = new CargoViewModel();
        public CitiesViewModel Cities { get; } = new CitiesViewModel();
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
            _allSlots = list;
            ApplyGameFilter();
        }

        /// <summary>按当前游戏重新筛选档案列表。</summary>
        private void ApplyGameFilter()
        {
            var groups = _allSlots
                .Where(s => string.Equals(s.Game, SelectedGame, StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => s.ProfileId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new SaveProfileItem
                {
                    Game = g.First().Game,
                    ProfileId = g.First().ProfileId,
                    IsSteam = g.Any(x => x.IsSteam),
                    Slots = g.OrderBy(x => x.SlotName, StringComparer.OrdinalIgnoreCase).ToList()
                })
                .OrderBy(p => p.ProfileId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? previous = SelectedProfile?.ProfileId;
            Profiles = new ObservableCollection<SaveProfileItem>(groups);
            SelectedProfile = Profiles.FirstOrDefault(p =>
                               string.Equals(p.ProfileId, previous, StringComparison.OrdinalIgnoreCase))
                           ?? Profiles.FirstOrDefault();

            UpdateSlotStatus();
        }

        /// <summary>刷新状态栏中与当前游戏相关的存档统计。</summary>
        private void UpdateSlotStatus()
        {
            if (Profiles.Count == 0)
            {
                StatusText = $"未在{GameLabel(SelectedGame)}中找到存档档案，可点击「选择 .sii 文件…」手动加载。";
            }
            else
            {
                int slots = Profiles.Sum(p => p.Slots.Count);
                StatusText = $"{GameLabel(SelectedGame)}：已发现 {slots} 个存档槽，分布在 {Profiles.Count} 个档案中。";
            }
        }

        /// <summary>游戏的显示名称。</summary>
        private static string GameLabel(string game) =>
            string.Equals(game, "ATS", StringComparison.OrdinalIgnoreCase) ? "美卡 1" : "欧卡 2";

        partial void OnSelectedGameChanged(string value) => ApplyGameFilter();

        /// <summary>存档载入/换档后刷新工具栏上依赖它的显示状态。</summary>
        partial void OnCurrentSaveChanged(SaveFile? value)
        {
            OnPropertyChanged(nameof(HasLoadedSave));
            OnPropertyChanged(nameof(LoadedPathText));
            OnPropertyChanged(nameof(SaveFormatText));
            OnPropertyChanged(nameof(SaveFormatTip));
        }

        partial void OnSelectedProfileChanged(SaveProfileItem? value)
        {
            Slots = value != null
                ? new ObservableCollection<SaveSlot>(value.Slots)
                : new ObservableCollection<SaveSlot>();
            SelectedSlot = Slots.FirstOrDefault();
            OnPropertyChanged(nameof(ProfileDisplay));
        }

        partial void OnSelectedSlotChanged(SaveSlot? value)
        {
            var files = value != null
                ? new SaveLocationFinder().ListSiiFiles(value.FullPath)
                : new List<string>();
            SlotFiles = new ObservableCollection<SlotFileItem>(files.Select(f => new SlotFileItem
            {
                Name = Path.GetFileName(f),
                FullPath = f
            }));
            OnPropertyChanged(nameof(SlotDisplay));
        }

        /// <summary>加载当前选中存档槽中的主存档文件。</summary>
        [RelayCommand]
        private void LoadSelectedSlot()
        {
            if (SelectedSlot == null)
            {
                _dialogs.ShowMessage("请先在右侧选择要加载的存档槽。");
                return;
            }

            var files = new SaveLocationFinder().ListSiiFiles(SelectedSlot.FullPath);
            if (files.Count == 0)
            {
                _dialogs.ShowMessage("该存档槽下没有找到 .sii 文件。");
                return;
            }

            var target = files.Find(f => string.Equals(Path.GetFileName(f), "game.sii", StringComparison.OrdinalIgnoreCase))
                         ?? files.Find(f => string.Equals(Path.GetFileName(f), "economy.sii", StringComparison.OrdinalIgnoreCase))
                         ?? files[0];
            LoadFile(target);
        }

        /// <summary>在资源管理器中打开当前存档槽所在文件夹。</summary>
        [RelayCommand]
        private void OpenSlotFolder()
        {
            if (SelectedSlot == null)
            {
                _dialogs.ShowMessage("请先在右侧选择存档槽。");
                return;
            }
            try
            {
                if (!Directory.Exists(SelectedSlot.FullPath))
                {
                    _dialogs.ShowMessage("文件夹不存在：" + SelectedSlot.FullPath);
                    return;
                }
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + SelectedSlot.FullPath + "\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("无法打开文件夹：" + ex.Message);
            }
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
                Cities.LoadFrom(sf);
                StatusText = "已加载：" + path;
                UpdateLoadedInfo();
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("打开失败：" + ex.Message);
                StatusText = "打开失败";
            }
        }

        /// <summary>刷新右侧信息区中已加载存档的概要。</summary>
        private void UpdateLoadedInfo()
        {
            if (CurrentSave == null || !CurrentSave.IsLoaded)
            {
                LoadedFileName = "（尚未加载）";
                LoadedSummary = "请在右侧选择档案与存档槽，然后点击「加载存档」。";
                return;
            }

            LoadedFileName = Path.GetFileName(CurrentSave.Path);
            LoadedSummary =
                $"格式：{FormatText(CurrentSave.Format)}" + Environment.NewLine +
                $"单元：{CurrentSave.Document?.Units.Count ?? 0} 个" + Environment.NewLine +
                $"金钱：{Profile.Money:N0}" + Environment.NewLine +
                $"经验：{Profile.Experience:N0}（{Profile.LevelSummary}）";
        }

        /// <summary>把存档格式枚举转换为中文说明。</summary>
        private static string FormatText(SiiFormat format)
        {
            switch (format)
            {
                case SiiFormat.Plaintext: return "明文 SII";
                case SiiFormat.Zlib: return "zlib 压缩 SII";
                case SiiFormat.Encrypted: return "ScsC 加密存档（已自动解密）";
                case SiiFormat.Binary: return "BSII 二进制存档（已自动解码）";
                default: return "未知格式";
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
                StatusText = "已保存（明文 SII）：" + CurrentSave.Path;

                // 保存后目标文件已是明文，格式标记要跟着变（⚠ → 明文 SII）。
                OnPropertyChanged(nameof(SaveFormatText));
                OnPropertyChanged(nameof(SaveFormatTip));

                _dialogs.ShowMessage(BuildSaveMessage());
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
                StatusText = "已另存为（明文 SII）：" + path;

                // 另存为后目标文件也是明文，格式标记同步更新。
                OnPropertyChanged(nameof(SaveFormatText));
                OnPropertyChanged(nameof(SaveFormatTip));

                _dialogs.ShowMessage("另存为成功。" + Environment.NewLine + BuildSaveMessage());
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("保存失败：" + ex.Message);
            }
        }

        /// <summary>
        /// Builds the message shown after a successful save. The game cannot
        /// read re-encrypted containers, therefore the file is written as
        /// plaintext SII and the original (encrypted) file is backed up.
        /// </summary>
        private string BuildSaveMessage()
        {
            string message = "已保存为明文 SII 格式，游戏可直接读取该存档。";
            if (!string.IsNullOrEmpty(CurrentSave?.LastBackupPath))
            {
                message += Environment.NewLine + "原加密存档已备份为：" + CurrentSave.LastBackupPath;
            }
            return message;
        }

        [RelayCommand]
        private void Exit()
        {
            Application.Current.Shutdown();
        }
    }
}
