using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ets2SaveEditor.Core.Domain.TypedUnits;
using Ets2SaveEditor.Core.Persistence;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain
{
    /// <summary>
    /// Aggregates a single <c>.sii</c> save file: its format, parsed document
    /// and convenient typed accessors. Loading never writes to disk; saving
    /// serializes and (optionally) backs up the original first.
    /// </summary>
    public class SaveFile
    {
        /// <summary>File system path of the save file.</summary>
        public string Path { get; set; }

        /// <summary>
        /// Container format currently on disk. After <see cref="Save"/> it is
        /// always <see cref="SiiFormat.Plaintext"/> because saves are written
        /// back as plaintext <c>SiiNunit</c> (the game re-encrypts on its next
        /// autosave). Before saving it matches <see cref="LoadedFormat"/>.
        /// </summary>
        public SiiFormat Format { get; set; }

        /// <summary>
        /// Container format the file had when it was loaded. Provided for
        /// display/informational purposes only — it no longer influences how
        /// the document is written. Saves are always written back as plaintext
        /// <c>SiiNunit</c> (see <see cref="Save"/>): the game reads plaintext
        /// without performing an HMAC check and re-encrypts the file on its
        /// next autosave. An encrypted ScsC save is therefore written back as
        /// plaintext, not as an encrypted ScsC save.
        /// </summary>
        public SiiFormat LoadedFormat { get; private set; }

        /// <summary>
        /// Path of the backup created by the last successful save, or null when
        /// no backup was written.
        /// </summary>
        public string LastBackupPath { get; private set; }

        /// <summary>Parsed SII document.</summary>
        public SiiDocument Document { get; set; }

        /// <summary>Whether a document is currently loaded.</summary>
        public bool IsLoaded { get; set; }

        public SaveFile()
        {
        }

        public SaveFile(string path)
        {
            Path = path;
        }

        /// <summary>
        /// Loads and parses the save file at <paramref name="path"/> (or the
        /// previously set <see cref="Path"/>). Throws <see cref="SiiFormatException"/>
        /// on any failure and never modifies the original file.
        /// </summary>
        public void Load(string path)
        {
            if (!string.IsNullOrEmpty(path)) Path = path;
            if (string.IsNullOrEmpty(Path))
            {
                throw new SiiFormatException("No save file path specified.");
            }

            byte[] raw;
            try
            {
                raw = File.ReadAllBytes(Path);
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                throw new SiiFormatException("Failed to read save file: " + Path, ex);
            }

            Format = SiiFormatDetector.Detect(raw);
            LoadedFormat = Format;
            LastBackupPath = null;

            string text;
            SiiSchema schema;
            try
            {
                text = SiiFormatDetector.Decode(raw, Format, out schema);
            }
            catch (SiiFormatException)
            {
                IsLoaded = false;
                throw;
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                throw new SiiFormatException("Failed to decode save file: " + Path, ex);
            }

            // 二进制存档自带结构定义（权威）；明文存档退回内置表。
            if (schema == null || schema.UnitTypeCount == 0)
            {
                schema = BuiltInSiiSchema.Instance;
            }

            try
            {
                Document = SiiParser.Parse(text, schema);
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                throw new SiiFormatException("Failed to parse SII document: " + Path, ex);
            }

            IsLoaded = true;
        }

        /// <summary>
        /// Serializes the loaded document and writes it to <paramref name="path"/>
        /// (or the previously set <see cref="Path"/>).
        /// <para>
        /// The document is <b>always written back as plaintext <c>SiiNunit</c>
        /// text</b>, regardless of how it was originally stored (encrypted
        /// ScsC, zlib, binary or already plaintext). The game does not perform
        /// an HMAC check on plaintext saves and reads them directly; it then
        /// re-encrypts the file on its next autosave. Re-encoding an edited
        /// ScsC container with the original HMAC/IV would produce an invalid
        /// HMAC (the key is not public), causing the game to silently fall back
        /// to <c>profile.bak.sii</c> and discard every edit — so plaintext
        /// write-back is the only correct approach for an external editor.
        /// </para>
        /// <para>
        /// When the target file is (or was) an encrypted container, a
        /// timestamped backup is created first — no matter what
        /// <paramref name="backup"/> says — because the original encrypted file
        /// is the only irreplaceable copy (its HMAC key is not public); the
        /// backup lets the user restore the original if needed.
        /// </para>
        /// </summary>
        public void Save(string path, bool backup)
        {
            if (!IsLoaded || Document == null)
            {
                throw new SiiFormatException("Cannot save: no document is loaded.");
            }

            string target = string.IsNullOrEmpty(path) ? Path : path;
            if (string.IsNullOrEmpty(target))
            {
                throw new SiiFormatException("Cannot save: no target path specified.");
            }

            string serialized;
            try
            {
                serialized = SiiSerializer.Serialize(Document);
            }
            catch (Exception ex)
            {
                // Never write the original file if serialization fails.
                throw new SiiFormatException("Failed to serialize document; original file left untouched.", ex);
            }

            // 关键：无论原存档是加密(ScsC)/zlib/二进制/明文，保存时一律写回**明文 SiiNunit**。
            // ScsC 容器的 HMAC 密钥由 SCS 保密，外部工具无法为改动后的内容生成合法 HMAC；
            // 若原样写回加密容器（复用旧 HMAC/IV），游戏加载时 HMAC 校验失败会静默回退到
            // profile.bak.sii，导致所有修改不生效。明文 SiiNunit 游戏不做 HMAC 校验、可直接读取，
            // 并在下次自动保存时用合法 HMAC 重新加密成 ScsC（业界 SII_Decrypt / easy-scsmodmanager
            // 等公认做法）。
            SiiFormat targetFormat = SiiFormat.Plaintext;

            try
            {
                // 即使写回明文，若目标原本是加密容器，覆盖前仍强制备份（保留可还原的原始副本）。
                bool needsBackup = backup || IsEncryptedContainer(target);
                string backupPath = needsBackup ? BackupService.Backup(target) : null;

                WriteAtomically(target, serialized);

                LastBackupPath = backupPath;
                Format = targetFormat;
            }
            catch (Exception ex)
            {
                throw new SiiFormatException("Failed to write save file: " + target, ex);
            }

            Path = target;
        }

        /// <summary>
        /// Writes the file atomically: the content is first written to a
        /// temporary file in the same directory and then moved over the
        /// target. A crash or power loss therefore leaves the target either
        /// with the complete old content or with the complete new content —
        /// never with a truncated file.
        /// </summary>
        private static void WriteAtomically(string target, string content)
        {
            var encoding = new UTF8Encoding(false);
            WriteAtomically(target, encoding.GetBytes(content));
        }

        /// <summary>
        /// Byte-oriented overload of <see cref="WriteAtomically(string, string)"/>,
        /// used for binary containers (ScsC / zlib) that must not be re-encoded
        /// by a text writer.
        /// </summary>
        private static void WriteAtomically(string target, byte[] content)
        {
            // 注意：本类有实例属性 Path，需用 System.IO.Path 显式访问静态方法。
            string directory = System.IO.Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            // 临时文件必须与目标同目录，保证移动是同一卷内的原子操作。
            string tempPath = System.IO.Path.Combine(directory, System.IO.Path.GetFileName(target) + ".tmp");

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(content, 0, content.Length);
                    stream.Flush(true); // 落盘后再替换目标文件
                }

                if (File.Exists(target))
                {
                    // 同一卷内原子替换（Windows: MoveFileEx + REPLACE_EXISTING）。
                    File.Move(tempPath, target, overwrite: true);
                }
                else
                {
                    File.Move(tempPath, target);
                }
            }
            catch
            {
                // 失败时清理临时文件，目标文件保持原样。
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                    // 清理失败不影响原始错误。
                }
                throw;
            }
        }

        /// <summary>
        /// Checks whether the file at <paramref name="path"/> is a "ScsC"
        /// encrypted container. Such a file must be backed up before it is
        /// replaced by the plaintext SII output, because the container cannot
        /// be recreated (its HMAC key is not public).
        /// </summary>
        private static bool IsEncryptedContainer(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var header = new byte[4];
                if (stream.Read(header, 0, 4) < 4) return false;

                return header[0] == (byte)'S' && header[1] == (byte)'c' &&
                       header[2] == (byte)'s' && header[3] == (byte)'C';
            }
            catch
            {
                return false;
            }
        }

        // ---- Convenience typed accessors ----

        public Bank GetBank() => Bank.FromDocument(Document);
        public Player GetPlayer() => Player.FromDocument(Document);
        public List<Garage> GetGarages() => Garage.AllFromDocument(Document);
        public List<Vehicle> GetVehicles() => Vehicle.AllFromDocument(Document);
        public List<Trailer> GetTrailers() => Trailer.AllFromDocument(Document);
        public Economy GetEconomy() => Economy.FromDocument(Document);
        public List<JobOffer> GetFreightJobs() => JobOffer.AllFromDocument(Document);
    }
}
