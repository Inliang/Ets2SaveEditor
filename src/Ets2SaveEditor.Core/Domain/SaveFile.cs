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

        /// <summary>Detected container format.</summary>
        public SiiFormat Format { get; set; }

        /// <summary>
        /// Container format the file had when it was loaded. Used to decide
        /// whether the original (encrypted) file must be backed up before it
        /// is replaced by the plaintext SII output.
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
            try
            {
                text = SiiFormatDetector.Decode(raw, Format);
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

            try
            {
                Document = SiiParser.Parse(text);
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
        /// The game cannot read a re-encrypted container (the HMAC key is not
        /// public), so the file is always written as plaintext SII, which the
        /// game engine reads natively. When the original file was encrypted, a
        /// timestamped backup of it is created first — no matter what
        /// <paramref name="backup"/> says — so the original data is never lost.
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

            try
            {
                // 加密存档必须先备份：明文输出无法再变回加密容器（HMAC 密钥未公开）。
                bool needsBackup = backup || IsEncryptedContainer(target);
                string backupPath = needsBackup ? BackupService.Backup(target) : null;

                WriteAtomically(target, serialized);

                LastBackupPath = backupPath;
                Format = SiiFormat.Plaintext;
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
                var encoding = new UTF8Encoding(false);
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, encoding))
                {
                    writer.Write(content);
                    writer.Flush();
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
