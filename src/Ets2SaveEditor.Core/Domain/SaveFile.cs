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
        /// how the document is re-encoded on save so that the original
        /// container type is preserved (an encrypted ScsC save is written
        /// back as an encrypted ScsC save, not as plaintext).
        /// </summary>
        public SiiFormat LoadedFormat { get; private set; }

        /// <summary>
        /// Original 32-byte HMAC region ([4..36)) of the loaded ScsC container.
        /// It is preserved verbatim on save because the HMAC key is not public
        /// and cannot be recomputed; the game ignores it on load. Null unless
        /// the loaded file was encrypted.
        /// </summary>
        private byte[] _originalHmac;

        /// <summary>
        /// Original 16-byte IV ([36..52)) of the loaded ScsC container. It is
        /// reused as the AES IV when re-encoding so the cryptographic metadata
        /// stays identical to what the game produced. Null unless the loaded
        /// file was encrypted.
        /// </summary>
        private byte[] _originalIv;

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
            _originalHmac = null;
            _originalIv = null;

            // 保留加密容器（ScsC）的原始头部：32 字节 HMAC + 16 字节 IV。
            // 重新编码时原样写回 HMAC、复用 IV，使文件与游戏原生写出的容器
            // 在头部结构上完全一致（只有密文区真正变化）。
            if (Format == SiiFormat.Encrypted && raw.Length >= SiiFormatDetector.CiphertextOffset)
            {
                _originalHmac = new byte[SiiFormatDetector.HmacLength];
                Array.Copy(raw, SiiFormatDetector.HmacOffset, _originalHmac, 0, SiiFormatDetector.HmacLength);

                _originalIv = new byte[SiiFormatDetector.IvLength];
                Array.Copy(raw, SiiFormatDetector.IvOffset, _originalIv, 0, SiiFormatDetector.IvLength);
            }

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
        /// The document is re-encoded into the <b>same container format it was
        /// loaded in</b> so the game can read the result unchanged:
        /// <list type="bullet">
        ///   <item><description><see cref="SiiFormat.Encrypted"/> (ScsC): the
        ///   plaintext SII is zlib-compressed then AES-256-CBC encrypted with
        ///   the well-known SCS key and the <b>original IV</b>, and wrapped in
        ///   a ScsC container whose HMAC region is preserved verbatim. This
        ///   produces a file byte-for-byte structurally identical to one the
        ///   game writes, so the game reads it natively.</description></item>
        ///   <item><description><see cref="SiiFormat.Zlib"/>: re-zlib-compressed
        ///   and written as a bare zlib stream.</description></item>
        ///   <item><description><see cref="SiiFormat.Plaintext"/> / <see cref="SiiFormat.Binary"/>
        ///   / unknown: written as plaintext SII text (a binary save therefore
        ///   degrades to plaintext — re-encoding BSII is out of scope for this
        ///   fix).</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// When the target file is (or was) an encrypted container, a
        /// timestamped backup is created first — no matter what
        /// <paramref name="backup"/> says — because the original encrypted file
        /// is the only irreplaceable copy (its HMAC key is not public).
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

            // 把明文 SII 文本转成与加载时一致的容器字节。
            byte[] rawBytes = null;
            SiiFormat targetFormat = SiiFormat.Plaintext;

            byte[] plainBytes = new UTF8Encoding(false).GetBytes(serialized);
            switch (LoadedFormat)
            {
                case SiiFormat.Encrypted:
                    if (_originalIv == null)
                    {
                        throw new SiiFormatException(
                            "无法写回加密存档：缺少原始 IV。请重新加载该存档后再保存。");
                    }
                    rawBytes = SiiFormatDetector.EncodeEncryptedContainer(plainBytes, _originalHmac, _originalIv);
                    targetFormat = SiiFormat.Encrypted;
                    break;

                case SiiFormat.Zlib:
                    rawBytes = SiiFormatDetector.EncodeZlibContainer(plainBytes);
                    targetFormat = SiiFormat.Zlib;
                    break;

                default:
                    // Plaintext / Binary / Unknown → 保持明文 SII。
                    rawBytes = null;
                    targetFormat = SiiFormat.Plaintext;
                    break;
            }

            try
            {
                // 加密存档必须先备份：写回加密档会覆盖唯一的原始副本，
                // 而 HMAC 密钥未公开、无法从明文再精确重建。
                bool needsBackup = backup || LoadedFormat == SiiFormat.Encrypted || IsEncryptedContainer(target);
                string backupPath = needsBackup ? BackupService.Backup(target) : null;

                if (rawBytes != null)
                {
                    WriteAtomically(target, rawBytes);
                }
                else
                {
                    WriteAtomically(target, serialized);
                }

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
