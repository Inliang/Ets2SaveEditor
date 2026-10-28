using System;
using System.IO;

namespace Ets2SaveEditor.Core.Persistence
{
    /// <summary>
    /// Creates timestamped backups of save files before they are overwritten.
    /// </summary>
    public static class BackupService
    {
        /// <summary>
        /// Copies the file at <paramref name="path"/> to
        /// <c>&lt;path&gt;.bak.&lt;yyyyMMddHHmmss&gt;</c>. Returns the backup
        /// path, or null when the source file does not exist.
        /// </summary>
        public static string Backup(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string backupPath = path + ".bak." + timestamp;
            File.Copy(path, backupPath, true);
            return backupPath;
        }
    }
}
