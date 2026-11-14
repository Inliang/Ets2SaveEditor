using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Persistence
{
    /// <summary>
    /// Describes a located save slot on disk.
    /// </summary>
    public class SaveSlot
    {
        /// <summary>"ETS2" or "ATS".</summary>
        public string Game { get; set; }

        /// <summary>Profile directory name (e.g. a SteamID or profile ID).</summary>
        public string ProfileId { get; set; }

        /// <summary>Save slot directory name.</summary>
        public string SlotName { get; set; }

        /// <summary>Full path to the save slot directory.</summary>
        public string FullPath { get; set; }

        /// <summary>Whether this slot was discovered via Steam (vs local documents).</summary>
        public bool IsSteam { get; set; }

        /// <summary>槽位下 <c>game.sii</c> 的最后修改时间；取不到时为 <see cref="DateTime.MinValue"/>。</summary>
        public DateTime LastWriteTime { get; set; } = DateTime.MinValue;

        /// <summary>同槽 <c>info.sii</c> 里 <c>name:</c> 字段的内容（例如 "2026.11.13"），取不到为空串。</summary>
        public string SaveName { get; set; } = string.Empty;

        /// <summary>是否为游戏日志里最后一次加载的存档槽（由 <c>GameLogReader</c> 标记）。</summary>
        public bool IsLastLoaded { get; set; }

        /// <summary>槽名是否以 <c>autosave</c> 开头（含 autosave_drive_* 一类）。</summary>
        public bool IsAutoSave { get; set; }

        /// <summary>槽位类型的可读标签。</summary>
        private string TypeLabel
        {
            get
            {
                if (string.Equals(SlotName, "autosave", StringComparison.OrdinalIgnoreCase)) return "游戏继续游戏读取";
                if (string.Equals(SlotName, "quicksave", StringComparison.OrdinalIgnoreCase)) return "快速存档";
                if (SlotName != null && SlotName.StartsWith("autosave_drive", StringComparison.OrdinalIgnoreCase)) return "自动驾驶存档";
                return "手动存档";
            }
        }

        /// <summary>
        /// 可读的槽位标签：槽名 + 类型标签 + 时间 + 存档名（+ 上次加载标记 / Steam），
        /// 例如 <c>autosave · 游戏继续游戏读取 · 11-14 20:22</c>。
        /// </summary>
        public string Display
        {
            get
            {
                string time = LastWriteTime == DateTime.MinValue
                    ? "时间未知"
                    : LastWriteTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
                string label = $"{SlotName} · {TypeLabel} · {time}";
                if (!string.IsNullOrEmpty(SaveName)) label += " · " + SaveName;
                if (IsLastLoaded) label += " · 游戏上次加载（已默认选中）";
                if (IsSteam) label += " · Steam";
                return label;
            }
        }

        /// <summary>
        /// 返回纯槽名。下拉框已改用可读的 <see cref="Display"/>；
        /// 这里保留纯槽名返回值，供其它仍依赖 <c>ToString()</c> 的调用方使用。
        /// </summary>
        public override string ToString() => SlotName;

        public SaveSlot()
        {
            Game = string.Empty;
            ProfileId = string.Empty;
            SlotName = string.Empty;
            FullPath = string.Empty;
            IsSteam = false;
        }
    }

    /// <summary>
    /// Locates ETS2 / ATS save slots in the local "My Documents" folder and
    /// under Steam's userdata directory. All registry access is wrapped so that
    /// missing permissions degrade gracefully (empty result instead of a crash).
    /// </summary>
    public class SaveLocationFinder
    {
        private static readonly string[] LocalGameFolders = new string[]
        {
            "Euro Truck Simulator 2",
            "American Truck Simulator"
        };

        private const string Ets2AppId = "227300";
        private const string AtsAppId = "270880";

        /// <summary>Local ("My Documents") save slots for ETS2 and ATS.</summary>
        public List<SaveSlot> FindLocalProfiles()
        {
            var result = new List<SaveSlot>();
            string docs;
            try
            {
                docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            catch
            {
                return result;
            }
            if (string.IsNullOrEmpty(docs)) return result;

            foreach (var gameFolder in LocalGameFolders)
            {
                string profilesRoot = Path.Combine(docs, gameFolder, "profiles");
                string game = gameFolder.StartsWith("American", StringComparison.OrdinalIgnoreCase) ? "ATS" : "ETS2";
                CollectSlots(profilesRoot, game, false, result);
            }
            return result;
        }

        /// <summary>
        /// Steam save slots discovered via the Valve registry key. Returns an
        /// empty list when the registry is unavailable.
        /// </summary>
        public List<SaveSlot> FindSteamProfiles()
        {
            var result = new List<SaveSlot>();
            string steamPath = NativeRegistry.GetStringValue(@"Software\Valve\Steam", "SteamPath");
            if (string.IsNullOrEmpty(steamPath)) return result;

            string userdata = Path.Combine(steamPath, "userdata");
            if (!Directory.Exists(userdata)) return result;

            try
            {
                foreach (var uidDir in Directory.GetDirectories(userdata))
                {
                    // ETS2 (227300) — primary focus of v1.
                    CollectSlots(Path.Combine(uidDir, Ets2AppId, "remote", "profiles"), "ETS2", true, result);
                    // ATS (270880) — noted for completeness; same layout.
                    CollectSlots(Path.Combine(uidDir, AtsAppId, "remote", "profiles"), "ATS", true, result);
                }
            }
            catch
            {
                // Ignore inaccessible directories.
            }
            return result;
        }

        /// <summary>Returns all <c>*.sii</c> files directly inside a slot directory.</summary>
        public List<string> ListSiiFiles(string slotPath)
        {
            var files = new List<string>();
            if (string.IsNullOrEmpty(slotPath) || !Directory.Exists(slotPath)) return files;
            try
            {
                foreach (var f in Directory.GetFiles(slotPath, "*.sii", SearchOption.TopDirectoryOnly))
                {
                    files.Add(f);
                }
            }
            catch
            {
                // Ignore inaccessible directories.
            }
            return files;
        }

        private static void CollectSlots(string profilesRoot, string game, bool isSteam, List<SaveSlot> result)
        {
            if (!Directory.Exists(profilesRoot)) return;
            try
            {
                foreach (var profileDir in Directory.GetDirectories(profilesRoot))
                {
                    string profileId = Path.GetFileName(profileDir);
                    string saveRoot = Path.Combine(profileDir, "save");
                    if (!Directory.Exists(saveRoot)) continue;

                    foreach (var slotDir in Directory.GetDirectories(saveRoot))
                    {
                        string slotName = Path.GetFileName(slotDir);

                        // 槽位元数据：game.sii 的 mtime（默认选中优先级用），info.sii 的 name。
                        DateTime lastWrite = DateTime.MinValue;
                        try
                        {
                            string gameSii = Path.Combine(slotDir, "game.sii");
                            if (File.Exists(gameSii)) lastWrite = File.GetLastWriteTime(gameSii);
                        }
                        catch
                        {
                            // 尽力而为，取不到时间就保持 MinValue。
                        }
                        TryReadSaveName(slotDir, out string saveName);

                        result.Add(new SaveSlot
                        {
                            Game = game,
                            ProfileId = profileId,
                            SlotName = slotName,
                            FullPath = slotDir,
                            IsSteam = isSteam,
                            LastWriteTime = lastWrite,
                            SaveName = saveName,
                            IsAutoSave = slotName.StartsWith("autosave", StringComparison.OrdinalIgnoreCase),
                            IsLastLoaded = false
                        });
                    }
                }
            }
            catch
            {
                // Ignore inaccessible directories.
            }
        }

        /// <summary>
        /// 尽力而为地读取槽位目录里 <c>info.sii</c> 的 <c>name: "..."</c> 字段
        /// （例如 "2026.11.13"），用于帮用户在游戏菜单里对上存档名。
        /// 兼容 <c>ScsC</c> 加密容器与明文 <c>SiiNunit</c> 两种 info.sii；
        /// 任何异常、文件缺失或找不到字段都返回 false，绝不抛异常。
        /// </summary>
        public static bool TryReadSaveName(string slotPath, out string name)
        {
            name = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(slotPath)) return false;
                string infoPath = Path.Combine(slotPath, "info.sii");
                if (!File.Exists(infoPath)) return false;

                byte[] raw = File.ReadAllBytes(infoPath);
                SiiFormat format = SiiFormatDetector.Detect(raw);
                string text = SiiFormatDetector.Decode(raw, format);
                if (string.IsNullOrEmpty(text)) return false;

                foreach (string rawLine in text.Split('\n'))
                {
                    string line = rawLine.Trim();
                    if (!line.StartsWith("name:", StringComparison.OrdinalIgnoreCase)) continue;
                    string value = line.Substring("name:".Length).Trim();
                    if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    {
                        name = value.Substring(1, value.Length - 2);
                    }
                    else
                    {
                        name = value;
                    }
                    return !string.IsNullOrEmpty(name);
                }
                return false;
            }
            catch
            {
                name = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// Minimal, dependency-free wrapper around the Windows registry API,
        /// used to read the Steam install path. Resolves at runtime, so it is
        /// harmless on non-Windows (returns null).
        /// </summary>
        private static class NativeRegistry
        {
            private const int KEY_READ = 0x20019;
            private const int ERROR_SUCCESS = 0;
            private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(0x80000001);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, int ulOptions, int samDesired, out IntPtr phkResult);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, IntPtr lpReserved, out int lpType, StringBuilder lpData, ref int lpcbData);

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern int RegCloseKey(IntPtr hKey);

            public static string GetStringValue(string subKey, string valueName)
            {
                if (RegOpenKeyEx(HKEY_CURRENT_USER, subKey, 0, KEY_READ, out IntPtr hKey) != ERROR_SUCCESS)
                {
                    return null;
                }
                try
                {
                    int size = 512;
                    var sb = new StringBuilder(size);
                    int type;
                    int ret = RegQueryValueEx(hKey, valueName, IntPtr.Zero, out type, sb, ref size);
                    return ret == ERROR_SUCCESS ? sb.ToString() : null;
                }
                finally
                {
                    RegCloseKey(hKey);
                }
            }
        }
    }
}
