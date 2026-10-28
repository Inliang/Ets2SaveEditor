using System;
using System.Globalization;
using System.IO;
using Ets2SaveEditor.Core.Common;

namespace Ets2SaveEditor.Core.Persistence
{
    /// <summary>
    /// Creates timestamped backups of save files before they are overwritten.
    /// </summary>
    public static class BackupService
    {
        /// <summary>
        /// Maximum number of candidate file names tried when the preferred name
        /// is already taken.
        /// </summary>
        private const int MaxCandidates = 1000;

        /// <summary>
        /// Copies the file at <paramref name="path"/> to
        /// <c>&lt;path&gt;.bak.&lt;yyyyMMddHHmmssfff&gt;</c>. Returns the backup
        /// path, or null when the source file does not exist.
        /// <para>
        /// The generated name is guaranteed to be unique: the timestamp has
        /// millisecond precision and a numeric suffix (<c>-1</c>, <c>-2</c>, …)
        /// is appended while the name is already taken. Existing backups are
        /// <b>never</b> overwritten — the backup of an encrypted save is the
        /// only remaining copy of the original data (the container cannot be
        /// recreated because its HMAC key is not public), so losing it would
        /// mean losing the save.
        /// </para>
        /// </summary>
        public static string Backup(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            string timestamp = DateTime.Now.ToString(Constants.BackupTimestampFormat, CultureInfo.InvariantCulture);
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string fileName = Path.GetFileName(path);

            for (int index = 0; index < MaxCandidates; index++)
            {
                string suffix = index == 0
                    ? string.Empty
                    : "-" + index.ToString(CultureInfo.InvariantCulture);

                string candidate = Path.Combine(directory, fileName + ".bak." + timestamp + suffix);
                if (File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    // overwrite: false —— 绝不覆盖已存在的备份。
                    File.Copy(path, candidate, false);
                    return candidate;
                }
                catch (IOException)
                {
                    // 与其它进程 / 线程并发创建了同名文件：换下一个候选名。
                }
                catch (UnauthorizedAccessException)
                {
                    // 同上。
                }
            }

            throw new IOException("无法为存档创建唯一的备份文件名：" + path);
        }
    }
}
