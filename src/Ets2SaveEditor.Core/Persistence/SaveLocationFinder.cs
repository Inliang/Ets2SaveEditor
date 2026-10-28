using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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

        /// <summary>Human-friendly label for list boxes.</summary>
        public string Display => $"[{Game}] {ProfileId} / {SlotName}" + (IsSteam ? " (Steam)" : "");

        /// <summary>
        /// 存档槽名称。列表/下拉框中直接显示存档槽名即可，
        /// 因为同一个档案下的存档槽名唯一。
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
                        result.Add(new SaveSlot
                        {
                            Game = game,
                            ProfileId = profileId,
                            SlotName = Path.GetFileName(slotDir),
                            FullPath = slotDir,
                            IsSteam = isSteam
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
