using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Persistence;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.ViewModels;

namespace Ets2SaveEditor.Tests
{
    public static class Program
    {
        /// <summary>
        /// Real ETS2 save used for the optional integration test. The file is
        /// only ever read and copied to a temporary directory — it is never
        /// modified or overwritten.
        /// </summary>
        private static readonly string RealSavePath =
            @"C:\Users\Administrator\Documents\Euro Truck Simulator 2\profiles\4361726F6C696E65E4B8B6\save\1\game.sii";

        public static int Main()
        {
            int failures = 0;

            failures += TestRoundTrip();
            failures += TestEncrypted();
            failures += TestScsCContainer();
            failures += TestBsiiDecoder();
            failures += TestBackupUniqueness();
            failures += TestNilSentinel();
            failures += TestNewBsiiTypes();
            failures += TestAtomicSave();
            failures += TestStringStylePreserved();
            failures += TestRealSave();

            Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
            return failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------
        // 基础往返测试：使用真实存档中的字段名。
        // ------------------------------------------------------------------

        private static int TestRoundTrip()
        {
            int failures = 0;

            var sample =
                "SiiNunit\n{\n" +
                "economy : _nameless.0000 {\n experience_points: 5000\n long_dist: 2\n heavy: 1\n visited_cities: 2\n visited_cities[0]: city.odense\n visited_cities[1]: city.berlin\n}\n" +
                "bank : _nameless.0001 {\n money_account: 100000\n loan_limit: 0\n}\n" +
                "garage : _nameless.0002 {\n vehicles: 1\n vehicles[0]: null\n status: 3\n productivity: 0\n}\n" +
                "vehicle : _nameless.0003 {\n engine_wear: &3e117d8d\n chassis_wear: 0\n fuel_relative: 1\n}\n" +
                "trailer : _nameless.0005 {\n trailer_definition: trailer_def.scs.box.single_3.dryvan\n trailer_body_wear: 0\n}\n" +
                "job_offer_data : _nameless.0004 {\n target: \"sanbuilders.london\"\n cargo: cargo.haulie\n company_truck: scania_s_2024e\n urgency: 0\n units_count: 1\n fill_ratio: 0\n}\n}\n";

            string dir = Path.Combine(Path.GetTempPath(), "ets2test");
            Directory.CreateDirectory(dir);
            string inPath = Path.Combine(dir, "combined.sii");
            File.WriteAllText(inPath, sample);
            string outPath = Path.Combine(dir, "combined.out.sii");

            var sf = new SaveFile();
            sf.Load(inPath);

            var profile = new ProfileViewModel();
            profile.LoadFrom(sf);
            profile.Money = 500000;
            profile.Experience = 30000;
            profile.ApplyToModel();

            var garages = new GaragesViewModel();
            garages.LoadFrom(sf);
            int gcount = garages.Items.Count;
            if (gcount < 1) { Console.WriteLine("FAIL: no garages loaded"); failures++; }
            else { garages.BuyCommand.Execute(garages.Items[0]); }

            var truck = new TruckViewModel();
            truck.LoadFrom(sf);
            truck.RepairAllCommand.Execute(null);
            truck.RefuelAllCommand.Execute(null);

            var freight = new FreightMarketViewModel();
            freight.LoadFrom(sf);
            int jcount = freight.Items.Count;
            freight.CreateJobCommand.Execute(null);

            sf.Save(outPath, false);

            // Reload and verify persisted changes.
            var sf2 = new SaveFile();
            sf2.Load(outPath);
            var p2 = new ProfileViewModel(); p2.LoadFrom(sf2);
            var g2 = new GaragesViewModel(); g2.LoadFrom(sf2);
            var t2 = new TruckViewModel(); t2.LoadFrom(sf2);
            var f2 = new FreightMarketViewModel(); f2.LoadFrom(sf2);

            bool moneyOk = p2.Money == 500000;
            bool xpOk = p2.Experience == 30000;
            bool garageOk = g2.Items.Count == gcount && g2.Items[0].Status == 3;
            bool wearOk = t2.Items.Count > 0 && t2.Items[0].EngineWear == 0 && t2.Items[0].Fuel == 1.0;
            bool jobOk = f2.Items.Count == jcount + 1;

            Console.WriteLine($"[roundtrip] moneyOk={moneyOk} xpOk={xpOk} garageOk={garageOk} wearOk={wearOk} jobOk={jobOk}");
            if (!moneyOk) { Console.WriteLine("FAIL: money not persisted"); failures++; }
            if (!xpOk) { Console.WriteLine("FAIL: experience not persisted"); failures++; }
            if (!garageOk) { Console.WriteLine("FAIL: garage status not persisted"); failures++; }
            if (!wearOk) { Console.WriteLine("FAIL: repair/refuel not persisted"); failures++; }
            if (!jobOk) { Console.WriteLine("FAIL: new job not persisted"); failures++; }

            // 数组必须以 "name: count" + "name[i]: value" 的形式保存。
            string saved = File.ReadAllText(outPath);
            bool arrayOk = saved.Contains("visited_cities: 2") && saved.Contains("visited_cities[0]: city.odense");
            bool hexOk = !saved.Contains("0.7") && !saved.Contains("wear : ");
            Console.WriteLine($"[roundtrip] arrayOk={arrayOk} hexOk={hexOk}");
            if (!arrayOk) { Console.WriteLine("FAIL: array serialization wrong"); failures++; }
            if (!hexOk) { Console.WriteLine("FAIL: value formatting wrong"); failures++; }

            return failures;
        }

        // ------------------------------------------------------------------
        // 加密存档测试（ScsC 容器）。
        // ------------------------------------------------------------------

        private static int TestEncrypted()
        {
            int failures = 0;

            string plaintext =
                "SiiNunit\n{\n" +
                "economy : _nameless.0000 {\n experience_points: 4000\n}\n" +
                "bank : _nameless.0001 {\n money_account: 250000\n}\n" +
                "garage : _nameless.0002 {\n status: 3\n}\n" +
                "}\n";

            byte[] container = BuildScsCContainer(plaintext, out _);

            string dir = Path.Combine(Path.GetTempPath(), "ets2test");
            Directory.CreateDirectory(dir);
            string encPath = Path.Combine(dir, "encrypted.sii");
            File.WriteAllBytes(encPath, container);

            var sf = new SaveFile();
            try
            {
                sf.Load(encPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: encrypted save threw on load: " + ex.Message);
                return failures + 1;
            }

            bool formatOk = sf.Format == SiiFormat.Encrypted;
            bool parsedOk = sf.Document != null && sf.Document.Units.Count >= 3;
            var bank = sf.GetBank();
            bool moneyOk = bank != null && bank.Money == 250000;
            var player = sf.GetPlayer();
            bool xpOk = player != null && player.Experience == 4000;

            Console.WriteLine($"[encrypted] formatOk={formatOk} parsedOk={parsedOk} moneyOk={moneyOk} xpOk={xpOk} units={sf.Document?.Units.Count}");
            if (!formatOk) { Console.WriteLine("FAIL: encrypted format not detected"); failures++; }
            if (!parsedOk) { Console.WriteLine("FAIL: encrypted save not parsed"); failures++; }
            if (!moneyOk) { Console.WriteLine("FAIL: bank money not decoded from encrypted save"); failures++; }
            if (!xpOk) { Console.WriteLine("FAIL: experience not decoded from encrypted save"); failures++; }

            return failures;
        }

        /// <summary>
        /// 构造真实的 ScsC 容器（56 字节头 + 密文），载荷为明文 SiiNunit。
        /// 同时验证旧的 "SrcC" 魔数不再被识别（根因回归测试）。
        /// </summary>
        private static int TestScsCContainer()
        {
            int failures = 0;

            string plaintext =
                "SiiNunit\n{\n" +
                "economy : _nameless.0000 {\n experience_points: 777\n}\n" +
                "bank : _nameless.0001 {\n money_account: 58564997\n}\n" +
                "vehicle : _nameless.0002 {\n engine_wear: &3e117d8d\n fuel_relative: 1\n}\n" +
                "}\n";

            byte[] container = BuildScsCContainer(plaintext, out int plaintextLength);

            string dir = Path.Combine(Path.GetTempPath(), "ets2test");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "scsc.sii");
            File.WriteAllBytes(path, container);

            var sf = new SaveFile();
            try
            {
                sf.Load(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: ScsC container threw on load: " + ex.Message);
                return failures + 1;
            }

            bool magicOk = sf.Format == SiiFormat.Encrypted;
            bool headerOk = container[0] == (byte)'S' && container[1] == (byte)'c' &&
                            container[2] == (byte)'s' && container[3] == (byte)'C';
            bool lengthOk = BitConverter.ToUInt32(container, 52) == (uint)plaintextLength;
            bool moneyOk = sf.GetBank()?.Money == 58564997;
            bool xpOk = sf.GetPlayer()?.Experience == 777;
            double wear = sf.GetVehicles().Count > 0 ? sf.GetVehicles()[0].EngineWear : -1;

            Console.WriteLine($"[scsc] magicOk={magicOk} headerOk={headerOk} lengthOk={lengthOk} moneyOk={moneyOk} xpOk={xpOk} wear={wear}");
            if (!magicOk) { Console.WriteLine("FAIL: ScsC container not detected"); failures++; }
            if (!headerOk) { Console.WriteLine("FAIL: ScsC magic bytes wrong"); failures++; }
            if (!lengthOk) { Console.WriteLine("FAIL: ScsC plaintext length field wrong"); failures++; }
            if (!moneyOk) { Console.WriteLine("FAIL: money_account not decoded"); failures++; }
            if (!xpOk) { Console.WriteLine("FAIL: experience_points not decoded"); failures++; }
            if (Math.Abs(wear - 0.14f) > 0.01f) { Console.WriteLine("FAIL: engine_wear not decoded"); failures++; }

            // 回归：旧魔数 "SrcC" 必须不再被当成加密容器。
            byte[] wrong = (byte[])container.Clone();
            wrong[1] = (byte)'r';
            string wrongPath = Path.Combine(dir, "wrongmagic.sii");
            File.WriteAllBytes(wrongPath, wrong);
            bool wrongRejected = SiiFormatDetector.Detect(wrong) != SiiFormat.Encrypted;
            Console.WriteLine($"[scsc] wrong magic rejected={wrongRejected}");
            if (!wrongRejected) { Console.WriteLine("FAIL: legacy 'SrcC' magic still accepted"); failures++; }

            // 覆盖加密源文件时必须强制备份（即使 backup=false）。
            string saveTarget = Path.Combine(dir, "scsc_save.sii");
            File.Copy(path, saveTarget, true);
            var sfSave = new SaveFile();
            sfSave.Load(saveTarget);
            int backupsBefore = Directory.GetFiles(dir, "scsc_save.sii.bak.*").Length;
            sfSave.Save(saveTarget, false);
            int backupsAfter = Directory.GetFiles(dir, "scsc_save.sii.bak.*").Length;
            bool backupOk = backupsAfter == backupsBefore + 1 && !string.IsNullOrEmpty(sfSave.LastBackupPath);
            bool plainOk = SiiFormatDetector.Detect(File.ReadAllBytes(saveTarget)) == SiiFormat.Plaintext;
            Console.WriteLine($"[scsc] backupOk={backupOk} plainOk={plainOk}");
            if (!backupOk) { Console.WriteLine("FAIL: encrypted source file was not backed up"); failures++; }
            if (!plainOk) { Console.WriteLine("FAIL: saved file is not plaintext SII"); failures++; }

            return failures;
        }

        /// <summary>
        /// 组装 ScsC 容器：zlib 压缩 → AES-256-CBC 加密 → 56 字节头 + 密文。
        /// </summary>
        private static byte[] BuildScsCContainer(string plaintext, out int plaintextLength)
        {
            byte[] data = Encoding.UTF8.GetBytes(plaintext);

            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                {
                    z.Write(data, 0, data.Length);
                }
                compressed = ms.ToArray();
            }

            byte[] iv = { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
                          0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff, 0x01 };
            byte[] key =
            {
                0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6,
                0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
                0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c,
                0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a
            };
            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var enc = aes.CreateEncryptor();
                cipher = enc.TransformFinalBlock(compressed, 0, compressed.Length);
            }

            var container = new byte[56 + cipher.Length];
            container[0] = (byte)'S';
            container[1] = (byte)'c';
            container[2] = (byte)'s';
            container[3] = (byte)'C';
            // [4..36) 为 32 字节 HMAC，密钥未公开，读时忽略。
            Array.Copy(iv, 0, container, 36, 16);
            WriteUInt32LE(container, 52, (uint)data.Length);
            Array.Copy(cipher, 0, container, 56, cipher.Length);

            plaintextLength = data.Length;
            return container;
        }

        private static void WriteUInt32LE(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        // ------------------------------------------------------------------
        // BSII 二进制解码器测试。
        // ------------------------------------------------------------------

        /// <summary>
        /// 手工构造一个最小 BSII 载荷（1 个结构块 + 1 个数据块），验证解码文本完全正确。
        /// </summary>
        private static int TestBsiiDecoder()
        {
            int failures = 0;

            byte[] binary = BuildMinimalBsii();

            string decoded = SiiBsiiDecoder.Decode(binary);

            const string expected =
                "SiiNunit\r\n" +
                "{\r\n" +
                "test_unit : _nameless.278.bdc9.2018 {\r\n" +
                " count_value: 42\r\n" +
                " label: \"hello world\"\r\n" +
                " ref: null\r\n" +
                " ratio: &3e800000\r\n" +
                " numbers: 2\r\n" +
                " numbers[0]: 7\r\n" +
                " numbers[1]: 9\r\n" +
                "}\r\n" +
                "\r\n" +
                "}\r\n";

            bool exact = decoded == expected;
            Console.WriteLine($"[bsii] exact={exact}");
            if (!exact)
            {
                Console.WriteLine("FAIL: bsii decode mismatch");
                Console.WriteLine("--- expected ---\n" + expected);
                Console.WriteLine("--- actual ---\n" + decoded);
                failures++;
            }

            // 裸 BSII 文件应被识别为二进制格式并能正常加载。
            string dir = Path.Combine(Path.GetTempPath(), "ets2test");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "sample.bsii");
            File.WriteAllBytes(path, binary);

            var sf = new SaveFile();
            try
            {
                sf.Load(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: bsii file threw on load: " + ex.Message);
                return failures + 1;
            }

            bool formatOk = sf.Format == SiiFormat.Binary;
            bool valueOk = sf.Document.GetFirstByType("test_unit")?.GetLong("count_value") == 42;
            bool arrayOk = sf.Document.GetFirstByType("test_unit")?.GetArrayItems("numbers").Count == 2;
            Console.WriteLine($"[bsii] formatOk={formatOk} valueOk={valueOk} arrayOk={arrayOk}");
            if (!formatOk) { Console.WriteLine("FAIL: bsii format not detected"); failures++; }
            if (!valueOk) { Console.WriteLine("FAIL: bsii value not parsed"); failures++; }
            if (!arrayOk) { Console.WriteLine("FAIL: bsii array not parsed"); failures++; }

            return failures;
        }

        /// <summary>
        /// 构造最小 BSII 载荷：
        /// "BSII" + version(3)
        ///   结构块：id=1，名称 test_unit，字段 count_value(UINT32)、label(STRING)、
        ///           ref(ID)、ratio(FLOAT)、numbers(UINT32_ARRAY)
        ///   数据块：一个 nameless id、对应取值
        ///   结束块：validity = 0
        /// </summary>
        private static byte[] BuildMinimalBsii()
        {
            using var ms = new MemoryStream();

            // "BSII" + version
            ms.Write(Encoding.ASCII.GetBytes("BSII"), 0, 4);
            WriteUInt32(ms, 3);

            // ---- 结构定义块 ----
            WriteUInt32(ms, 0);          // blockType == 0 → 结构定义
            ms.WriteByte(1);             // validity
            WriteUInt32(ms, 1);          // structureId
            WriteString(ms, "test_unit");

            WriteUInt32(ms, 0x27);       // UINT32
            WriteString(ms, "count_value");
            WriteUInt32(ms, 0x01);       // STRING
            WriteString(ms, "label");
            WriteUInt32(ms, 0x39);       // ID
            WriteString(ms, "ref");
            WriteUInt32(ms, 0x05);       // FLOAT
            WriteString(ms, "ratio");
            WriteUInt32(ms, 0x28);       // UINT32_ARRAY
            WriteString(ms, "numbers");
            WriteUInt32(ms, 0);          // 字段列表结束

            // ---- 数据块 ----
            WriteUInt32(ms, 1);          // blockType == structureId
            ms.WriteByte(0xFF);          // nameless id
            WriteUInt64(ms, 0x278UL << 32 | 0xbdc9UL << 16 | 0x2018UL);
            WriteUInt32(ms, 42);         // count_value
            WriteString(ms, "hello world"); // label
            ms.WriteByte(0);             // ref：空 id → null
            WriteFloat(ms, 0.25f);       // ratio
            WriteUInt32(ms, 2);          // numbers：数量
            WriteUInt32(ms, 7);
            WriteUInt32(ms, 9);

            // ---- 结束块 ----
            WriteUInt32(ms, 0);
            ms.WriteByte(0);

            return ms.ToArray();
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        private static void WriteUInt64(Stream stream, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                stream.WriteByte((byte)(value >> (8 * i)));
            }
        }

        private static void WriteFloat(Stream stream, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            stream.Write(bytes, 0, 4);
        }

        private static void WriteString(Stream stream, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteUInt32(stream, (uint)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        // ------------------------------------------------------------------
        // 备份唯一性测试（P0-1 回归）。
        // ------------------------------------------------------------------

        /// <summary>
        /// 连续两次备份/保存必须产生**不同**的备份文件，且第一个备份的
        /// SHA-256 仍等于原始加密存档 —— 原始加密存档不可再生，备份是唯一安全网。
        /// </summary>
        private static int TestBackupUniqueness()
        {
            int failures = 0;

            string dir = Path.Combine(Path.GetTempPath(), "ets2test", "backup_uniqueness");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            byte[] container = BuildScsCContainer(
                "SiiNunit\n{\nbank : _nameless.0001 {\n money_account: 100\n}\n}\n", out _);
            string originalHash = Sha256(container);

            // ---- 1) BackupService 层：连续两次备份 ----
            string source = Path.Combine(dir, "source.sii");
            File.WriteAllBytes(source, container);

            string backup1 = BackupService.Backup(source);
            string backup2 = BackupService.Backup(source);

            bool serviceUnique = backup1 != null && backup2 != null &&
                                 !string.Equals(backup1, backup2, StringComparison.OrdinalIgnoreCase);
            bool serviceIntact = serviceUnique && backup1 != null &&
                                 Sha256(File.ReadAllBytes(backup1)) == originalHash;

            Console.WriteLine($"[backup] serviceUnique={serviceUnique} serviceIntact={serviceIntact}");
            if (!serviceUnique) { Console.WriteLine("FAIL: two backups share the same file name"); failures++; }
            if (!serviceIntact) { Console.WriteLine("FAIL: first backup was overwritten by the second one"); failures++; }

            // ---- 2) SaveFile 层：连续两次保存 ----
            string target = Path.Combine(dir, "game.sii");
            File.WriteAllBytes(target, container);

            var sf = new SaveFile();
            sf.Load(target);
            sf.Save(target, true);
            sf.Save(target, true);

            string[] backups = Directory.GetFiles(dir, "game.sii.bak.*");
            bool saveUnique = backups.Length == 2 &&
                              !string.Equals(backups[0], backups[1], StringComparison.OrdinalIgnoreCase);

            // 按名称排序即时间顺序（时间戳定长），最早的备份必须是原始加密文件。
            bool saveIntact = false;
            if (saveUnique)
            {
                string earliest = backups.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).First();
                saveIntact = Sha256(File.ReadAllBytes(earliest)) == originalHash;
            }

            Console.WriteLine($"[backup] saveUnique={saveUnique} saveIntact={saveIntact} backups={backups.Length}");
            if (!saveUnique) { Console.WriteLine("FAIL: expected exactly two distinct backups, got " + backups.Length); failures++; }
            if (!saveIntact) { Console.WriteLine("FAIL: the original encrypted save was not preserved in the first backup"); failures++; }

            // ---- 3) 加密源文件强制备份（backup=false 也必须备份）----
            string forcedTarget = Path.Combine(dir, "forced.sii");
            File.WriteAllBytes(forcedTarget, container);
            var sfForced = new SaveFile();
            sfForced.Load(forcedTarget);
            sfForced.Save(forcedTarget, false);
            string[] forcedBackups = Directory.GetFiles(dir, "forced.sii.bak.*");
            bool forcedOk = forcedBackups.Length == 1 &&
                            Sha256(File.ReadAllBytes(forcedBackups[0])) == originalHash;
            Console.WriteLine($"[backup] forcedBackup={forcedOk}");
            if (!forcedOk) { Console.WriteLine("FAIL: encrypted source file was not backed up automatically"); failures++; }

            return failures;
        }

        // ------------------------------------------------------------------
        // nil 哨兵测试（P1-2 回归）。
        // ------------------------------------------------------------------

        /// <summary>
        /// 无符号整型读到类型最大值时应输出 <c>nil</c>（游戏的"无值"哨兵），
        /// 而不是 4294967295 / 65535 这类退化数字。
        /// </summary>
        private static int TestNilSentinel()
        {
            int failures = 0;

            var writer = new BsiiWriter();
            writer.BeginStructure(1, "nil_unit");
            writer.Field(0x27, "max_u32");
            writer.Field(0x2b, "max_u16");
            writer.Field(0x33, "max_u64");
            writer.Field(0x27, "normal_u32");
            writer.EndFields();
            writer.BeginData(1);
            writer.NamelessId(0x278UL << 32 | 0xbdc9UL << 16 | 0x2018UL);
            writer.UInt32(0xFFFFFFFF);
            writer.UInt16(0xFFFF);
            writer.UInt64(ulong.MaxValue);
            writer.UInt32(42);
            writer.End();

            string decoded = SiiBsiiDecoder.Decode(writer.ToArray());

            const string expected =
                "SiiNunit\r\n" +
                "{\r\n" +
                "nil_unit : _nameless.278.bdc9.2018 {\r\n" +
                " max_u32: nil\r\n" +
                " max_u16: nil\r\n" +
                " max_u64: nil\r\n" +
                " normal_u32: 42\r\n" +
                "}\r\n" +
                "\r\n" +
                "}\r\n";

            bool exact = decoded == expected;
            bool hasNil = decoded.Contains(": nil\r\n");
            bool noMax = !decoded.Contains("4294967295") && !decoded.Contains("65535") &&
                         !decoded.Contains("18446744073709551615");
            bool normalKept = decoded.Contains("normal_u32: 42");

            Console.WriteLine($"[nil] exact={exact} hasNil={hasNil} noMax={noMax} normalKept={normalKept}");
            if (!exact)
            {
                Console.WriteLine("FAIL: nil sentinel decode mismatch");
                Console.WriteLine("--- expected ---\n" + expected);
                Console.WriteLine("--- actual ---\n" + decoded);
                failures++;
            }
            if (!hasNil) { Console.WriteLine("FAIL: decoded text contains no nil sentinel"); failures++; }
            if (!noMax) { Console.WriteLine("FAIL: decoded text still contains the raw type maximum"); failures++; }
            if (!normalKept) { Console.WriteLine("FAIL: ordinary unsigned value was mangled"); failures++; }

            // 往返幂等：解析后再序列化必须与解码文本完全一致。
            var doc = SiiParser.Parse(decoded);
            string reserialized = SiiSerializer.Serialize(doc);
            bool idempotent = reserialized == decoded;
            Console.WriteLine($"[nil] idempotent={idempotent}");
            if (!idempotent)
            {
                Console.WriteLine("FAIL: nil round-trip is not idempotent");
                Console.WriteLine("--- decoded ---\n" + decoded);
                Console.WriteLine("--- reserialized ---\n" + reserialized);
                failures++;
            }

            return failures;
        }

        // ------------------------------------------------------------------
        // 新增 BSII 类型覆盖测试（P2-1 回归）。
        // ------------------------------------------------------------------

        /// <summary>
        /// 覆盖 0x08（Vec2f 数组）、0x29（Int16）、0x2a（Int16 数组）、
        /// 0x3e（Id 数组）四种此前缺失的类型。
        /// </summary>
        private static int TestNewBsiiTypes()
        {
            int failures = 0;

            var writer = new BsiiWriter();
            writer.BeginStructure(1, "types_unit");
            writer.Field(0x08, "vec2s");
            writer.Field(0x29, "i16");
            writer.Field(0x2a, "i16s");
            writer.Field(0x3e, "ids");
            writer.EndFields();
            writer.BeginData(1);
            writer.NamelessId(0x278UL << 32 | 0xbdc9UL << 16 | 0x2018UL);

            writer.UInt32(2);           // vec2s：元素数量
            writer.Float(1f);
            writer.Float(2f);
            writer.Float(0f);
            writer.Float(0f);

            writer.Int16(-5);           // i16

            writer.UInt32(2);           // i16s：元素数量
            writer.Int16(-5);
            writer.Int16(7);

            writer.UInt32(2);           // ids：元素数量
            writer.NamedId("ab");
            writer.EmptyId();

            writer.End();

            string decoded = SiiBsiiDecoder.Decode(writer.ToArray());

            const string expected =
                "SiiNunit\r\n" +
                "{\r\n" +
                "types_unit : _nameless.278.bdc9.2018 {\r\n" +
                " vec2s: 2\r\n" +
                " vec2s[0]: (1, 2)\r\n" +
                " vec2s[1]: (0, 0)\r\n" +
                " i16: -5\r\n" +
                " i16s: 2\r\n" +
                " i16s[0]: -5\r\n" +
                " i16s[1]: 7\r\n" +
                " ids: 2\r\n" +
                " ids[0]: ab\r\n" +
                " ids[1]: null\r\n" +
                "}\r\n" +
                "\r\n" +
                "}\r\n";

            bool exact = decoded == expected;
            Console.WriteLine($"[types] exact={exact}");
            if (!exact)
            {
                Console.WriteLine("FAIL: new BSII types decode mismatch");
                Console.WriteLine("--- expected ---\n" + expected);
                Console.WriteLine("--- actual ---\n" + decoded);
                failures++;
            }

            return failures;
        }

        // ------------------------------------------------------------------
        // 原子保存测试（P2-2 回归）。
        // ------------------------------------------------------------------

        /// <summary>
        /// 保存必须走"临时文件 + 原子替换"，保存完成后目录里不能残留 .tmp 文件。
        /// </summary>
        private static int TestAtomicSave()
        {
            int failures = 0;

            string dir = Path.Combine(Path.GetTempPath(), "ets2test", "atomic_save");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            string source = Path.Combine(dir, "source.sii");
            File.WriteAllText(source,
                "SiiNunit\n{\nbank : _nameless.0001 {\n money_account: 42\n}\n}\n");

            string target = Path.Combine(dir, "game.sii");
            File.Copy(source, target, true);

            var sf = new SaveFile();
            sf.Load(target);
            sf.GetBank().Money = 999;
            sf.Save(target, false);

            string[] tempFiles = Directory.GetFiles(dir, "*.tmp");
            bool noTemp = tempFiles.Length == 0;
            bool targetOk = File.Exists(target) && File.ReadAllText(target).Contains("money_account: 999");
            bool reloadOk = false;
            try
            {
                var reload = new SaveFile();
                reload.Load(target);
                reloadOk = reload.GetBank()?.Money == 999;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: reloading the saved file threw: " + ex.Message);
            }

            Console.WriteLine($"[atomic] noTemp={noTemp} targetOk={targetOk} reloadOk={reloadOk}");
            if (!noTemp) { Console.WriteLine("FAIL: temporary file left behind: " + string.Join(",", tempFiles)); failures++; }
            if (!targetOk) { Console.WriteLine("FAIL: target file not written correctly"); failures++; }
            if (!reloadOk) { Console.WriteLine("FAIL: saved file cannot be reloaded"); failures++; }

            return failures;
        }

        // ------------------------------------------------------------------
        // 字符串表示风格保真测试（P2-3 回归）。
        // ------------------------------------------------------------------

        /// <summary>
        /// 编辑字符串类字段时必须保持原属性的表示风格：裸写仍是裸写，带引号仍带引号。
        /// </summary>
        private static int TestStringStylePreserved()
        {
            int failures = 0;

            string dir = Path.Combine(Path.GetTempPath(), "ets2test", "style");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "style.sii");
            File.WriteAllText(path,
                "SiiNunit\n{\n" +
                "job_offer_data : _nameless.0001 {\n" +
                " target: \"sanbuilders.london\"\n" +
                " company_truck: scania_s_2024e\n" +
                " cargo: cargo.haulie\n" +
                "}\n}\n");

            var sf = new SaveFile();
            sf.Load(path);

            var job = sf.GetFreightJobs()[0];
            job.TargetCity = "new.target";
            job.Truck = "scania_r_2024x";
            job.Cargo = "cargo.new";

            sf.Save(path, false);
            string saved = File.ReadAllText(path);

            bool quotedKept = saved.Contains("target: \"new.target\"");
            bool bareKept = saved.Contains("company_truck: scania_r_2024x");
            bool cargoBareKept = saved.Contains("cargo: cargo.new");
            bool noUnexpectedQuote = !saved.Contains("\"scania_r_2024x\"") && !saved.Contains("\"cargo.new\"");

            Console.WriteLine($"[style] quotedKept={quotedKept} bareKept={bareKept} cargoBareKept={cargoBareKept} noUnexpectedQuote={noUnexpectedQuote}");
            if (!quotedKept) { Console.WriteLine("FAIL: quoted string lost its quotes"); failures++; }
            if (!bareKept) { Console.WriteLine("FAIL: bare reference gained quotes"); failures++; }
            if (!cargoBareKept) { Console.WriteLine("FAIL: bare cargo id gained quotes"); failures++; }
            if (!noUnexpectedQuote) { Console.WriteLine("FAIL: values were re-quoted unnecessarily"); failures++; }

            return failures;
        }

        /// <summary>Computes the SHA-256 of the given bytes as a hex string.</summary>
        private static string Sha256(byte[] data)
        {
            byte[] hash = SHA256.HashData(data);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Small helper that builds binary BSII payloads for the tests.
        /// </summary>
        private sealed class BsiiWriter
        {
            private readonly MemoryStream _stream = new MemoryStream();

            public BsiiWriter()
            {
                _stream.Write(Encoding.ASCII.GetBytes("BSII"), 0, 4);
                UInt32(3); // version
            }

            public void BeginStructure(uint id, string name)
            {
                UInt32(0);      // blockType == 0 → 结构定义块
                _stream.WriteByte(1); // validity
                UInt32(id);
                Str(name);
            }

            public void Field(uint typeId, string name)
            {
                UInt32(typeId);
                Str(name);
            }

            public void EndFields() => UInt32(0);

            public void BeginData(uint structureId) => UInt32(structureId);

            public void NamelessId(ulong value)
            {
                _stream.WriteByte(0xFF);
                UInt64(value);
            }

            /// <summary>Writes a named id consisting of a single encoded part.</summary>
            public void NamedId(string part)
            {
                _stream.WriteByte(1);
                UInt64(EncodeString(part));
            }

            /// <summary>Writes an empty (null) id.</summary>
            public void EmptyId() => _stream.WriteByte(0);

            public void End()
            {
                UInt32(0);
                _stream.WriteByte(0);
            }

            public void UInt32(uint value)
            {
                _stream.WriteByte((byte)value);
                _stream.WriteByte((byte)(value >> 8));
                _stream.WriteByte((byte)(value >> 16));
                _stream.WriteByte((byte)(value >> 24));
            }

            public void UInt64(ulong value)
            {
                for (int i = 0; i < 8; i++)
                {
                    _stream.WriteByte((byte)(value >> (8 * i)));
                }
            }

            public void UInt16(ushort value)
            {
                _stream.WriteByte((byte)value);
                _stream.WriteByte((byte)(value >> 8));
            }

            public void Int16(short value) => UInt16(unchecked((ushort)value));

            public void Float(float value)
            {
                byte[] bytes = BitConverter.GetBytes(value);
                _stream.Write(bytes, 0, 4);
            }

            public void Str(string value)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                UInt32((uint)bytes.Length);
                _stream.Write(bytes, 0, bytes.Length);
            }

            public byte[] ToArray() => _stream.ToArray();

            /// <summary>
            /// Encodes a string the way the game does: the digits are stored
            /// least significant first, using the 38 character alphabet
            /// (digit 0 is unused, so digit d maps to table[d - 1]).
            /// </summary>
            private static ulong EncodeString(string text)
            {
                ulong value = 0;
                for (int i = text.Length - 1; i >= 0; i--)
                {
                    value = value * 38 + DigitOf(text[i]);
                }
                return value;
            }

            private static ulong DigitOf(char c)
            {
                if (c >= '0' && c <= '9') return (ulong)(c - '0' + 1);
                if (c >= 'a' && c <= 'z') return (ulong)(c - 'a' + 11);
                if (c == '_') return 37;
                throw new ArgumentException("Unsupported character in encoded string: " + c);
            }
        }

        // ------------------------------------------------------------------
        // 真实存档集成测试（可选，文件不存在时跳过）。
        // ------------------------------------------------------------------

        /// <summary>
        /// 只读复制真实存档到临时目录后加载，验证关键字段可被正确读出。
        /// 绝不写入或覆盖用户真实存档。
        /// </summary>
        private static int TestRealSave()
        {
            if (!File.Exists(RealSavePath))
            {
                Console.WriteLine("[realsave] SKIP: real save not found");
                return 0;
            }

            int failures = 0;
            string dir = Path.Combine(Path.GetTempPath(), "ets2test");
            Directory.CreateDirectory(dir);
            string copy = Path.Combine(dir, "real_game_copy.sii");
            File.Copy(RealSavePath, copy, true);

            var sf = new SaveFile();
            try
            {
                sf.Load(copy);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: real save threw on load: " + ex.Message);
                return failures + 1;
            }

            bool formatOk = sf.Format == SiiFormat.Encrypted;
            long money = sf.GetBank()?.Money ?? -1;
            long xp = sf.GetPlayer()?.Experience ?? -1;
            bool moneyOk = money == 54356889;
            bool xpOk = xp == 252508;

            Console.WriteLine($"[realsave] formatOk={formatOk} money={money} xp={xp} units={sf.Document.Units.Count}");
            if (!formatOk) { Console.WriteLine("FAIL: real save format not detected"); failures++; }
            if (!moneyOk) { Console.WriteLine("FAIL: real money_account wrong: " + money); failures++; }
            if (!xpOk) { Console.WriteLine("FAIL: real experience_points wrong: " + xp); failures++; }

            return failures;
        }
    }
}
