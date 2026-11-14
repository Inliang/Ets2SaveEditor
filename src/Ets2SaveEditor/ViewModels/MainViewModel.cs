using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Persistence;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Services;

// 让独立的测试程序集能调用下面几个 internal 纯函数（默认槽选择 / 进程检测 / 提示文案）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Ets2SaveEditor.Tests")]

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
        /// 本工具自 v0.0.16 起保存时统一写回**明文 <c>SiiNunit</c>**：无论原存档是加密
        /// <c>ScsC</c>、zlib 还是二进制，保存后都是明文 SII 文本。
        /// </para>
        /// <para>
        /// 明文改值能被游戏读到，是**社区长期实测有效的经验性兼容**（SCS 论坛 2024-05-20
        /// 有「主 game.sii 解密成明文 → 改值 → 加载生效」的直接实测），并非 SCS 官方
        /// 承诺的既定行为；前提是**游戏已完全重启、且该存档未被本次会话缓存读过**
        /// （ETS2 1.50+ filesystem cache）。「工具改了但游戏里没变化」的头号根因不是
        /// 格式，而是缓存与选错槽位，故保存确认框与成功提示会给出完整的五步生效指引。
        /// </para>
        /// <para>
        /// 例外：原加密档在覆盖前会**强制备份**成 <c>.bak.&lt;时间戳&gt;</c>，因为原始加密
        /// 容器的 HMAC 密钥未公开、不可再生，备份是唯一的还原手段。
        /// </para>
        /// <para>标记基于 <c>LoadedFormat</c>（加载时的原始格式），未加载时为空串。</para>
        /// </summary>
        public string SaveFormatText
        {
            get
            {
                if (CurrentSave == null || !CurrentSave.IsLoaded) return string.Empty;

                return CurrentSave.LoadedFormat switch
                {
                    SiiFormat.Plaintext => "存档格式：明文 SII",
                    SiiFormat.Encrypted => "存档格式：加密 ScsC（保存后转明文 SII）",
                    SiiFormat.Zlib => "存档格式：zlib 压缩（保存后转明文 SII）",
                    SiiFormat.Binary => "存档格式：BSII 二进制（保存后转明文）",
                    _ => string.Empty,
                };
            }
        }

        /// <summary>格式标记的悬停说明：讲清保存后的实际格式与备份策略。</summary>
        public string SaveFormatTip
        {
            get
            {
                if (CurrentSave == null || !CurrentSave.IsLoaded) return string.Empty;

                switch (CurrentSave.LoadedFormat)
                {
                    case SiiFormat.Encrypted:
                        return "此存档为游戏原生的 ScsC 加密格式。本工具保存时会写回**明文 SII（SiiNunit）**；" +
                               "明文改值生效是社区长期实测的经验性兼容（SCS 论坛 2024-05-20 实测），并非官方既定行为，" +
                               "且须游戏已完全重启、该存档未被本次会话缓存读过（ETS2 1.50+ 缓存）。" +
                               "原加密文件会自动备份为 .bak.<时间戳>，可随时还原。";

                    case SiiFormat.Zlib:
                        return "此存档为 zlib 压缩格式。本工具保存时会统一写回**明文 SII（SiiNunit 文本）**，" +
                               "原文件会自动备份为 .bak.<时间戳>，可随时还原。";

                    case SiiFormat.Binary:
                        return "此存档为 BSII 二进制格式。本工具目前无法重新编码二进制，" +
                               "保存后会转为明文 SII（SiiNunit 文本），原文件会自动备份为" +
                               " .bak.<时间戳>，可随时还原。";

                    default:
                        return "此存档当前已是明文 SII 格式（通常是本工具保存的结果），" +
                               "保存后仍为明文 SII（SiiNunit 文本）。";
                }
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
            MarkLastLoadedSlot();
            ApplyGameFilter();
        }

        /// <summary>
        /// 游戏日志目录（<c>game.log.txt</c>）。ETS2 为「Euro Truck Simulator 2」，
        /// ATS 的文档目录是「American Truck Simulator」；额外兼容带「2」的旧写法。
        /// </summary>
        private string[] GetGameLogPaths()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.Equals(SelectedGame, "ATS", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    Path.Combine(docs, "American Truck Simulator", "game.log.txt"),
                    Path.Combine(docs, "American Truck Simulator 2", "game.log.txt")
                };
            }
            return new[] { Path.Combine(docs, "Euro Truck Simulator 2", "game.log.txt") };
        }

        /// <summary>
        /// 读取当前游戏的日志，把「最后加载的存档槽」标记到槽位元数据上，
        /// 供 <see cref="PickDefaultSlot"/> 与状态栏提示使用。
        /// </summary>
        private void MarkLastLoadedSlot()
        {
            foreach (var slot in _allSlots) slot.IsLastLoaded = false;
            try
            {
                foreach (string logPath in GetGameLogPaths())
                {
                    LastLoadedSave? last = GameLogReader.FindLastLoaded(logPath);
                    if (last == null) continue;

                    foreach (var slot in _allSlots)
                    {
                        if (string.Equals(slot.ProfileId, last.ProfileHex, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(slot.SlotName, last.SlotName, StringComparison.OrdinalIgnoreCase))
                        {
                            slot.IsLastLoaded = true;
                        }
                    }
                    break;
                }
            }
            catch
            {
                // 日志不可用不该影响槽位扫描。
            }
        }

        /// <summary>
        /// 默认槽选择的纯函数（便于测试）：
        /// ① 游戏上次加载的槽 &gt; ② 槽名恰为 autosave（忽略大小写） &gt;
        /// ③ game.sii 的 mtime 最新 &gt; ④ 字典序第一。空集合返回 null。
        /// </summary>
        internal static SaveSlot? PickDefaultSlot(IEnumerable<SaveSlot> slots)
        {
            if (slots == null) return null;
            var list = slots.Where(s => s != null).ToList();
            if (list.Count == 0) return null;

            return list
                .OrderByDescending(s => s.IsLastLoaded)
                .ThenByDescending(s => string.Equals(s.SlotName, "autosave", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(s => s.LastWriteTime)
                .ThenBy(s => s.SlotName, StringComparer.OrdinalIgnoreCase)
                .First();
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

                // 有「游戏上次加载」标记时，明确告诉用户默认选中的是哪一个。
                var lastLoaded = Profiles.SelectMany(p => p.Slots).FirstOrDefault(s => s.IsLastLoaded);
                if (lastLoaded != null)
                {
                    StatusText += $" 游戏上次加载的存档：{lastLoaded.SlotName}，已默认选中。";
                }
            }
        }

        /// <summary>游戏的显示名称。</summary>
        private static string GameLabel(string game) =>
            string.Equals(game, "ATS", StringComparison.OrdinalIgnoreCase) ? "美卡 1" : "欧卡 2";

        partial void OnSelectedGameChanged(string value)
        {
            // 切换游戏后按新游戏的日志重新标记，再筛选档案列表。
            MarkLastLoadedSlot();
            ApplyGameFilter();
        }

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
            // 不再固定选字典序第一（永远落到 "1"），改用智能默认；用户仍可手动切换。
            SelectedSlot = PickDefaultSlot(Slots);
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

        /// <summary>
        /// 防止误把元数据 / 备份文件当成可编辑存档加载。
        /// 拒绝：<c>info*.sii</c>、<c>*_backup.sii</c>、<c>*_bak.sii</c>、
        /// <c>*.bak.*</c> 以及 0 字节文件；主存档 <c>game.sii</c> / <c>economy.sii</c> 放行。
        /// <para>
        /// 采用「拒绝元数据/备份」而不是「只认两个固定文件名」：OpenFile / OpenFolder
        /// 允许用户显式打开自定义命名的存档，一刀切会误伤真实用法与既有用例；
        /// 槽位目录里实际存在的其它文件（info.sii、game_backup.sii、game.sii.bak.&lt;时间戳&gt;）
        /// 全部落在拒绝规则内。覆盖「双击文件列表」「打开文件」「打开文件夹」三条路径。
        /// </para>
        /// </summary>
        public static bool IsEditableSaveFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string name = Path.GetFileName(path);
                if (!name.EndsWith(".sii", StringComparison.OrdinalIgnoreCase)) return false;

                string lower = name.ToLowerInvariant();
                bool isMetadataOrBackup =
                    lower.StartsWith("info", StringComparison.Ordinal) ||
                    lower.Contains("_backup") ||
                    lower.Contains("_bak") ||
                    lower.Contains(".bak.");
                if (isMetadataOrBackup) return false;

                var info = new FileInfo(path);
                return info.Exists && info.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public void LoadFile(string path)
        {
            if (!IsEditableSaveFile(path))
            {
                _dialogs.ShowMessage("该文件是元数据或备份，不是可编辑存档，请点『加载存档』加载 game.sii");
                StatusText = "已取消加载：请选择 game.sii / economy.sii";
                return;
            }

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
                case SiiFormat.Zlib: return "zlib 压缩存档";
                case SiiFormat.Encrypted: return "ScsC 加密存档";
                case SiiFormat.Binary: return "BSII 二进制存档";
                default: return "未知格式";
            }
        }

        /// <summary>
        /// 从存档文件路径解析档位(profile)/槽位(slot)信息，用于保存前让用户确认
        /// 正在编辑的是哪个槽，避免「编错槽」（这是与「格式」不同的另一根因维度）。
        /// <para>
        /// 标准路径形如 <c>.../profiles/&lt;profileHex&gt;/save/&lt;slot&gt;/game.sii</c>：
        /// 槽位 = 紧邻 <c>game.sii</c> 的目录；档位 = 槽位再往上两级（即 profiles 下的目录）。
        /// 解析失败或非标准路径时给出尽力而为的结果（缺失部分返回空串）。纯函数，不触碰磁盘。
        /// </para>
        /// </summary>
        public static SaveSlotInfo ResolveSaveSlot(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return new SaveSlotInfo(string.Empty, string.Empty);
            }

            string? containingDir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(containingDir))
            {
                return new SaveSlotInfo(string.Empty, string.Empty);
            }

            string slot = Path.GetFileName(TrimTrailingSeparator(containingDir));

            // 档位 = 槽位目录再往上两级（即 profiles 下的目录）。
            string? parent = Path.GetDirectoryName(containingDir);
            string? grandparent = parent == null ? null : Path.GetDirectoryName(parent);
            string profileHex = string.IsNullOrEmpty(grandparent)
                ? string.Empty
                : Path.GetFileName(TrimTrailingSeparator(grandparent));

            return new SaveSlotInfo(profileHex, slot);
        }

        /// <summary>把 profile 目录名（十六进制）解码为可读档位名；失败则返回原 hex。</summary>
        public static string DecodeProfileName(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return hex ?? string.Empty;
            if (hex.Length % 2 != 0) return hex;

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!TryHexByte(hex, i * 2, out byte b)) return hex;
                bytes[i] = b;
            }

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>去掉路径末尾的目录分隔符，便于正确取末级目录名。</summary>
        private static string TrimTrailingSeparator(string s)
        {
            return s.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>解析单个十六进制字节（0-9 / a-f / A-F）。</summary>
        private static bool TryHexByte(string hex, int index, out byte value)
        {
            int hi = HexNibble(hex[index]);
            int lo = HexNibble(hex[index + 1]);
            if (hi < 0 || lo < 0)
            {
                value = 0;
                return false;
            }

            value = (byte)((hi << 4) | lo);
            return true;
        }

        private static int HexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        /// <summary>
        /// 存档档位/槽位解析结果。<see cref="ProfileHex"/> 为 profiles 下的十六进制目录名，
        /// <see cref="ProfileName"/> 为解码后的可读档位名（解码失败则为原 hex）。
        /// </summary>
        public sealed record SaveSlotInfo(string ProfileHex, string Slot)
        {
            public string ProfileName => DecodeProfileName(ProfileHex);
        }

        /// <summary>游戏进程名（<c>Process.GetProcessesByName</c> 不带 .exe 后缀）。</summary>
        private static readonly string[] GameProcessNames = { "eurotrucks2", "amtrucks2" };

        /// <summary>
        /// 纯函数：给定进程名列表，判断其中是否包含运行中的 ETS2 / ATS 游戏进程。
        /// 忽略大小写；null 集合 / null 元素都视为不命中。
        /// </summary>
        internal static bool IsGameRunning(IEnumerable<string> processNames)
        {
            if (processNames == null) return false;
            foreach (string? name in processNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                foreach (string game in GameProcessNames)
                {
                    if (string.Equals(name, game, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        /// <summary>通过 <c>Process.GetProcessesByName</c>（不带 .exe）检测游戏是否正在运行。</summary>
        private static bool DetectGameRunning()
        {
            try
            {
                var names = new List<string>();
                foreach (string game in GameProcessNames)
                {
                    foreach (var p in Process.GetProcessesByName(game))
                    {
                        names.Add(p.ProcessName);
                    }
                }
                return IsGameRunning(names);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 写盘前检查：游戏运行中只警告、不硬性阻断；用户可取消，取消时写状态栏提示。
        /// 返回 false 表示应放弃本次保存。
        /// </summary>
        private bool ConfirmGameNotRunning()
        {
            if (!DetectGameRunning()) return true;

            const string warning =
                "检测到《欧洲卡车模拟2》正在运行。ETS2 1.50 起游戏会缓存已加载的存档：\n" +
                "现在保存，磁盘文件虽写入成功，游戏仍会使用内存/缓存里的旧版本，退出时还可能把旧内容写回覆盖。修改几乎肯定不会生效。\n" +
                "正确做法：先完全退出游戏进程（退回主菜单不够）→ 用本工具保存 → 重启游戏。\n" +
                "仍要继续保存吗？";

            if (_dialogs.Confirm(warning, "游戏正在运行")) return true;
            StatusText = "已取消保存：请先完全退出游戏后再保存";
            return false;
        }

        /// <summary>保存确认框与保存成功提示共用的完整五步生效指引。</summary>
        internal const string PostSaveGuide =
            "ETS2 1.50+ 的存档缓存会让游戏优先使用已缓存的旧版本。请按以下步骤让修改生效：\n" +
            "1) 保存前完全退出游戏进程（不是退回主菜单）；\n" +
            "2) 重启游戏后不要点『继续游戏』（它读取 autosave / 缓存旧版）；\n" +
            "3) 到 ESC → 保存并加载 → 加载游戏，手动选中本次保存的槽位；autosave 类槽需勾选『Show autosaves』才可见；\n" +
            "4) 进入游戏确认修改生效后，在游戏内手动保存一次，让改动成为游戏当前状态，避免下次被缓存回滚；\n" +
            "5) 若该档案启用了 Steam Cloud，请先关闭云同步（游戏内 Main Menu → Edit Profile → 取消 Use Steam Cloud；Steam 客户端 → 库 → 右键游戏 → 属性 → 常规 → 关闭云同步）。云同步在游戏启动与退出时各执行一次，会用云端旧副本覆盖本地修改，与游戏是否正在运行无关。";

        /// <summary>把槽名映射成可读的槽位类型标签。</summary>
        internal static string DescribeSlotType(string slotName)
        {
            if (string.IsNullOrEmpty(slotName)) return "未知";
            if (string.Equals(slotName, "autosave", StringComparison.OrdinalIgnoreCase)) return "autosave（自动存档）";
            if (string.Equals(slotName, "quicksave", StringComparison.OrdinalIgnoreCase)) return "quicksave（快速存档）";
            if (slotName.StartsWith("autosave_drive", StringComparison.OrdinalIgnoreCase)) return "自动驾驶存档";
            return "手动存档";
        }

        /// <summary>构造保存确认框的完整文案（路径 / 槽位类型 / 存档名 / 五步指引）。</summary>
        private string BuildSaveConfirmMessage(SaveSlotInfo slotInfo)
        {
            var sb = new StringBuilder();
            if (SelectedSlot?.IsSteam == true)
            {
                sb.AppendLine("⚠️ 当前槽位来自 Steam Cloud 存档目录，请确认已关闭云同步，否则修改可能被云端覆盖。");
                sb.AppendLine();
            }
            sb.AppendLine("你正在把修改写回到以下存档：");
            sb.AppendLine("档位(profile)：" + slotInfo.ProfileName);
            sb.AppendLine("槽位(slot)：" + slotInfo.Slot);
            sb.AppendLine("槽位类型：" + DescribeSlotType(slotInfo.Slot));
            sb.AppendLine("完整路径：" + CurrentSave?.Path);
            if (!string.IsNullOrEmpty(SelectedSlot?.SaveName))
            {
                sb.AppendLine("存档名称：" + SelectedSlot.SaveName + "（在游戏菜单里按这个名字对号）");
            }
            sb.AppendLine();

            // 槽位类型分支保留，叠加在五步指引之上。
            if (string.Equals(slotInfo.Slot, "autosave", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("⚠️ 这是自动存档槽。游戏『继续游戏』通常会读它，但游戏下次自动保存会用新内容覆盖它。请确认这正是你要改的槽，避免改错槽导致『白改』。");
            }
            else
            {
                sb.AppendLine("这是手动存档槽，请在游戏内通过『保存并加载 → 加载游戏』手动选中它。");
            }
            sb.AppendLine();
            sb.AppendLine(PostSaveGuide);
            sb.AppendLine();
            sb.Append("确认保存到这个槽吗？");
            return sb.ToString();
        }

        [RelayCommand]
        private void Save()
        {
            if (CurrentSave == null || !CurrentSave.IsLoaded) { _dialogs.ShowMessage("请先打开一个存档文件。"); return; }

            // 写盘前检测游戏是否正在运行（可取消，不硬性阻断）。
            if (!ConfirmGameNotRunning()) return;

            // 保存前确认当前正在编辑的档位/槽位，避免用户改错槽（与「格式」是不同维度的根因）。
            SaveSlotInfo slotInfo = ResolveSaveSlot(CurrentSave.Path);
            string message = BuildSaveConfirmMessage(slotInfo);
            if (!_dialogs.Confirm(message, "确认保存位置"))
            {
                StatusText = "已取消保存（槽位确认被取消）";
                return;
            }

            try
            {
                Profile.ApplyToModel();
                Garages.ApplyToModel();
                Truck.ApplyToModel();
                Trailer.ApplyToModel();
                Freight.ApplyToModel();
                CurrentSave.Save(CurrentSave.Path, Settings.AutoBackup);
                StatusText = "已保存为明文 SII：" + CurrentSave.Path;

                // 保存一律写回明文 SiiNunit；此处刷新格式标记，使其与 v0.0.16 起
                // 「一律写明文」的实际落盘结果保持一致。
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

            // 写盘前检测游戏是否正在运行（可取消，不硬性阻断）。
            if (!ConfirmGameNotRunning()) return;

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
                StatusText = "已另存为明文 SII：" + path;

                // 另存为后目标文件格式同步更新（一律写明文 SiiNunit）。
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
        /// 保存成功后的提示。v0.0.16 起一律写回明文 <c>SiiNunit</c>；明文改值生效是
        /// 社区长期实测的经验性兼容（SCS 论坛 2024-05-20 实测），并非 SCS 官方既定行为，
        /// 前提是游戏已完全重启、且该存档未被本次会话缓存读过。因此这里完整展示
        /// 让修改生效的五步指引，而不只是说一句「已保存」。原加密容器覆盖前会备份。
        /// </summary>
        internal string BuildSaveMessage()
        {
            string message = "已保存为明文 SII（SiiNunit 文本）。" + Environment.NewLine +
                             Environment.NewLine + PostSaveGuide;
            if (!string.IsNullOrEmpty(CurrentSave?.LastBackupPath))
            {
                message += Environment.NewLine + "原始存档已备份为：" + CurrentSave.LastBackupPath;
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
