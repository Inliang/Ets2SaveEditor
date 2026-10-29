using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;
using SaveCompany = Ets2SaveEditor.Core.Domain.TypedUnits.Company;
using Ets2SaveEditor.Core.GameData;
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
            failures += TestCityVisiting();
            failures += TestCustomJobOffer();
            failures += TestLevelTable();
            failures += TestLevelInvariants();
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
                "job_offer_data : _nameless.0004 {\n target: \"sanbuilders.london\"\n cargo: cargo.haulie\n company_truck: scania_s_2024e\n urgency: 0\n units_count: 1\n fill_ratio: 0\n}\n" +
                "company : company.volatile.sanbuilders.london {\n permanent_data: company.permanent.sanbuilders\n job_offer: 1\n job_offer[0]: _nameless.0004\n cargo_offer_seeds: 1\n cargo_offer_seeds[0]: 1\n discovered: true\n state: 0\n}\n" +
                "}\n";

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
            job.Target = "new.target";
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

        /// <summary>
        /// 城市访问（visited_cities）测试：
        /// <list type="bullet">
        ///   <item><description>未访问的城市能被写入 visited_cities 数组；</description></item>
        ///   <item><description>重复添加会被拒绝；</description></item>
        ///   <item><description>移除城市后数组元素数正确减少；</description></item>
        ///   <item><description>保存后重新加载，新增城市仍在（往返一致）。</description></item>
        /// </list>
        /// </summary>
        private static int TestCityVisiting()
        {
            int failures = 0;

            string dir = Path.Combine(Path.GetTempPath(), "ets2test");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "cities.sii");

            File.WriteAllText(path,
                "SiiNunit\n{\n" +
                "economy : _nameless.0000 {\n" +
                " experience_points: 100\n" +
                " visited_cities: 2\n" +
                " visited_cities[0]: london\n" +
                " visited_cities[1]: berlin\n" +
                "}\n}\n");

            var sf = new SaveFile();
            try
            {
                sf.Load(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: city test save threw on load: " + ex.Message);
                return failures + 1;
            }

            var economy = sf.GetEconomy();
            if (economy == null)
            {
                Console.WriteLine("FAIL: economy unit not found");
                return failures + 1;
            }

            int initialCount = economy.VisitedCities.Count;
            bool initialOk = initialCount == 2;
            Console.WriteLine($"[cities] initial={initialCount}");
            if (!initialOk)
            {
                Console.WriteLine("FAIL: initial visited_cities count wrong: " + initialCount);
                failures++;
            }

            // 添加一个未访问的城市。
            bool added = economy.VisitCity("hamburg");
            int afterAdd = economy.VisitedCities.Count;
            bool addOk = added && afterAdd == 3 && economy.VisitedCities.Contains("hamburg");
            Console.WriteLine($"[cities] added={added} count={afterAdd}");
            if (!addOk)
            {
                Console.WriteLine("FAIL: VisitCity did not add the city");
                failures++;
            }

            // 重复添加应被拒绝。
            bool duplicate = economy.VisitCity("hamburg");
            int afterDuplicate = economy.VisitedCities.Count;
            bool dupOk = !duplicate && afterDuplicate == 3;
            Console.WriteLine($"[cities] duplicate={duplicate} count={afterDuplicate}");
            if (!dupOk)
            {
                Console.WriteLine("FAIL: duplicate city was added twice");
                failures++;
            }

            // 移除城市。
            bool removed = economy.UnvisitCity("berlin");
            int afterRemove = economy.VisitedCities.Count;
            bool removeOk = removed && afterRemove == 2 && !economy.VisitedCities.Contains("berlin");
            Console.WriteLine($"[cities] removed={removed} count={afterRemove}");
            if (!removeOk)
            {
                Console.WriteLine("FAIL: UnvisitCity did not remove the city");
                failures++;
            }

            // 保存并重新加载：新增的城市必须还在。
            try
            {
                sf.Save(path, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: city test save threw on save: " + ex.Message);
                return failures + 1;
            }

            var reloaded = new SaveFile();
            reloaded.Load(path);
            var economy2 = reloaded.GetEconomy();
            bool persisted = economy2 != null &&
                             economy2.VisitedCities.Count == 2 &&
                             economy2.VisitedCities.Contains("hamburg") &&
                             economy2.VisitedCities.Contains("london");
            Console.WriteLine($"[cities] persisted={persisted}");
            if (!persisted)
            {
                Console.WriteLine("FAIL: visited city did not survive save/reload");
                failures++;
            }

            // 内置城市目录应覆盖真实存档中出现的城市。
            bool catalogueOk = CityCatalogue.Find("london") != null &&
                               CityCatalogue.Find("hamburg") != null &&
                               CityCatalogue.Count > 300;
            Console.WriteLine($"[cities] catalogue={CityCatalogue.Count} ok={catalogueOk}");
            if (!catalogueOk)
            {
                Console.WriteLine("FAIL: city catalogue incomplete");
                failures++;
            }

            return failures;
        }

        // ------------------------------------------------------------------
        // 自定义货运任务：报酬估算 + 与公司的关联。
        // ------------------------------------------------------------------

        /// <summary>
        /// 验证 <c>job_offer_data</c> 的创建流程：
        /// 任务必须注册到 company 的 job_offer 数组，游戏才会在货运市场显示它；
        /// 同时验证报酬估算与实测费率一致。
        /// </summary>
        private static int TestCustomJobOffer()
        {
            int failures = 0;

            string dir = Path.Combine(Path.GetTempPath(), "ets2test");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "customjob.sii");

            File.WriteAllText(path,
                "SiiNunit\n{\n" +
                "company : company.volatile.stokes.erfurt {\n" +
                " permanent_data: company.permanent.stokes\n" +
                " job_offer: 1\n" +
                " job_offer[0]: _nameless.0001\n" +
                " cargo_offer_seeds: 2\n" +
                " cargo_offer_seeds[0]: 1\n" +
                " cargo_offer_seeds[1]: 2\n" +
                " discovered: true\n" +
                " state: 0\n" +
                "}\n" +
                "job_offer_data : _nameless.0001 {\n" +
                " target: \"stokes.erfurt\"\n" +
                " expiration_time: 0\n" +
                " urgency: 0\n" +
                " shortest_distance_km: 124\n" +
                " ferry_time: 0\n" +
                " ferry_price: 0\n" +
                " cargo: cargo.haulie\n" +
                " company_truck: scania_s_2024e\n" +
                " trailer_variant: trailer.scs_lowbed_3\n" +
                " trailer_definition: trailer_def.scs.lowbed.single_3.short\n" +
                " units_count: 4\n" +
                " fill_ratio: 1\n" +
                " trailer_place: 0\n" +
                "}\n" +
                "}\n");

            var sf = new SaveFile();
            try
            {
                sf.Load(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: custom job save threw on load: " + ex.Message);
                return failures + 1;
            }

            var company = SaveCompany.AllFromDocument(sf.Document).FirstOrDefault();
            if (company == null)
            {
                Console.WriteLine("FAIL: no company unit found in the save");
                return failures + 1;
            }

            bool companyOk = company != null &&
                             company.Brand == "stokes" &&
                             company.CityToken == "erfurt" &&
                             company.ShortReference == "stokes.erfurt" &&
                             company.JobOfferCount == 1;
            Console.WriteLine($"[customjob] companyOk={companyOk} ref={(company == null ? "<null>" : company.ShortReference)}");
            if (!companyOk)
            {
                Console.WriteLine("FAIL: company could not be read from the save");
                failures++;
            }

            var existing = sf.GetFreightJobs()[0];
            bool targetParsed = existing.TargetCity == "erfurt" && existing.TargetBrand == "stokes";
            Console.WriteLine($"[customjob] targetParsed={targetParsed} city={existing.TargetCity} brand={existing.TargetBrand}");
            if (!targetParsed)
            {
                Console.WriteLine("FAIL: target reference was not split into brand and city");
                failures++;
            }

            // 新建任务：必须同时创建单元并注册到公司。
            int offersBefore = sf.GetFreightJobs().Count;
            int companyOffersBefore = company!.JobOfferCount;

            var created = JobOffer.CreateAndRegister(
                sf.Document, company, "cargo.haulie", "scania_s_2024e", 300, 24, 1);
            if (created == null)
            {
                Console.WriteLine("FAIL: CreateAndRegister returned null");
                return failures + 1;
            }

            bool createdOk = created != null &&
                            sf.GetFreightJobs().Count == offersBefore + 1 &&
                            company.JobOfferCount == companyOffersBefore + 1 &&
                            company.JobOfferIds.Contains(created.Unit.Id) &&
                            created.Target == "stokes.erfurt" &&
                            created.DistanceKm == 300 &&
                            created.UnitsCount == 24 &&
                            created.Urgency == 1;
            Console.WriteLine($"[customjob] createdOk={createdOk} offers={company.JobOfferCount}");
            if (!createdOk)
            {
                Console.WriteLine("FAIL: new job offer was not registered with the company");
                failures++;
            }

            // 重复注册同一任务应被拒绝。
            bool duplicate = company!.AddJobOffer(created!.Unit.Id);
            bool dupOk = !duplicate && company.JobOfferCount == companyOffersBefore + 1;
            Console.WriteLine($"[customjob] duplicateRejected={dupOk}");
            if (!dupOk)
            {
                Console.WriteLine("FAIL: duplicate job offer registration was accepted");
                failures++;
            }

            // 报酬估算：与真实存档中实测的费率一致。
            // 真实存档记录：cargo.gummy_bears，144 km，33 单位，收入 4791 €，
            // 对应费率 4791 / (144 × 33) = 1.0082 €/单位/km。
            long estimate = JobIncomeEstimator.Estimate("cargo.gummy_bears", 144, 33, 0, 1.0);
            bool estimateOk = Math.Abs(estimate - 4791L) <= 40L;
            Console.WriteLine($"[customjob] estimate={estimate} ok={estimateOk}");
            if (!estimateOk)
            {
                Console.WriteLine("FAIL: income estimate does not match the recorded revenue");
                failures++;
            }

            // 未知货物使用默认费率；紧急度倍率必须大于 1。
            double unknownRate = JobIncomeEstimator.RateOf("cargo.does_not_exist");
            bool defaultOk = Math.Abs(unknownRate - CargoCatalogue.DefaultRate) < 0.0001;
            bool urgencyOk = JobIncomeEstimator.UrgencyFactor(0) < JobIncomeEstimator.UrgencyFactor(2);
            Console.WriteLine($"[customjob] defaultRate={unknownRate} defaultOk={defaultOk} urgencyOk={urgencyOk}");
            if (!defaultOk)
            {
                Console.WriteLine("FAIL: unknown cargo does not use the default rate");
                failures++;
            }
            if (!urgencyOk)
            {
                Console.WriteLine("FAIL: urgency does not increase the income");
                failures++;
            }

            // 保存后重新加载：任务与公司的关联必须还在。
            try
            {
                sf.Save(path, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: custom job save threw on save: " + ex.Message);
                return failures + 1;
            }

            var reloaded = new SaveFile();
            reloaded.Load(path);
            var company2 = SaveCompany.AllFromDocument(reloaded.Document).FirstOrDefault();
            var job2 = reloaded.GetFreightJobs().FirstOrDefault(j => j.Unit.Id == created.Unit.Id);
            bool persisted = company2 != null &&
                             company2.JobOfferCount == companyOffersBefore + 1 &&
                             job2 != null &&
                             company2.JobOfferIds.Contains(job2.Unit.Id);
            Console.WriteLine($"[customjob] persisted={persisted}");
            if (!persisted)
            {
                Console.WriteLine("FAIL: job offer registration did not survive save/reload");
                failures++;
            }

            // 删除任务：必须同时注销公司与单元。
            bool removed = job2 != null && job2.Remove();
            var company3 = SaveCompany.AllFromDocument(reloaded.Document).FirstOrDefault();
            bool removeOk = removed &&
                            company3 != null &&
                            company3.JobOfferCount == companyOffersBefore &&
                            !reloaded.GetFreightJobs().Any(j => j.Unit.Id == created.Unit.Id);
            Console.WriteLine($"[customjob] removeOk={removeOk}");
            if (!removeOk)
            {
                Console.WriteLine("FAIL: job offer was not unregistered from the company");
                failures++;
            }

            return failures;
        }

        // ------------------------------------------------------------------
        // 真实存档集成测试（可选，文件不存在时跳过）。
        // ------------------------------------------------------------------

        /// <summary>
        /// 只读复制真实存档到临时目录后加载，验证关键字段可被正确读出。
        /// 绝不写入或覆盖用户真实存档。
        /// </summary>
        // ------------------------------------------------------------------
        // 等级换算表测试。
        //
        // ETS2 不存等级，只存累计经验值，等级由 XP 按游戏自己的曲线换算。
        // 曲线取自游戏安装目录 def.scs 内的 xp_level_data.level_xp[]（增量语义），
        // 30 级之后沿用最后一项 6800，累加到 L100 恰为 575700。
        // 旧版本用的是一张凭猜测硬编码的表，把 52 级显示成了 20/21 级。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验等级换算表的正确性：真实存档实测值、边界、往返一致性，
        /// 以及与公开等级表的逐级交叉比对。
        /// </summary>
        private static int TestLevelTable()
        {
            int failures = 0;

            // 1) 用户真实存档实测：XP=252508 在游戏内显示 52 级。
            const int realXp = 252508;
            int realLevel = Player.LevelFromXp(realXp);
            bool realOk = realLevel == 52;
            Console.WriteLine($"[level] xp={realXp} -> level={realLevel} (expect 52)");
            if (!realOk)
            {
                Console.WriteLine("FAIL: real save XP should map to level 52, got " + realLevel);
                failures++;
            }

            // 2) 边界条件。
            CheckLevel(failures, "LevelFromXp(0)", Player.LevelFromXp(0), 1);
            CheckLevel(failures, "LevelFromXp(199)", Player.LevelFromXp(199), 1);
            CheckLevel(failures, "LevelFromXp(200)", Player.LevelFromXp(200), 1);
            CheckLevel(failures, "LevelFromXp(699)", Player.LevelFromXp(699), 1);
            CheckLevel(failures, "LevelFromXp(700)", Player.LevelFromXp(700), 2);
            CheckLevel(failures, "LevelFromXp(575700)", Player.LevelFromXp(575700), 100);
            CheckLevel(failures, "LevelFromXp(999999999)", Player.LevelFromXp(999999999), 100);

            // 3) XpForLevel 语义：升到该等级所需的累计 XP。
            CheckXp(failures, "XpForLevel(1)", Player.XpForLevel(1), 200);
            CheckXp(failures, "XpForLevel(2)", Player.XpForLevel(2), 700);
            CheckXp(failures, "XpForLevel(3)", Player.XpForLevel(3), 1400);
            CheckXp(failures, "XpForLevel(0)", Player.XpForLevel(0), 0);
            CheckXp(failures, "XpForLevel(101)", Player.XpForLevel(101), 575700);

            // 4) 往返一致性：对每个等级都应能换算回去。
            int roundTripBad = 0;
            for (int lv = 1; lv <= Player.MaxLevel; lv++)
            {
                if (Player.LevelFromXp(Player.XpForLevel(lv)) != lv) roundTripBad++;
            }
            Console.WriteLine($"[level] roundTrip mismatches = {roundTripBad}");
            if (roundTripBad != 0)
            {
                Console.WriteLine("FAIL: round-trip mismatch for " + roundTripBad + " level(s)");
                failures++;
            }

            // 5) 与公开等级表逐级交叉比对（L1..L17）。
            int[] levels = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 };
            long[] expected =
            {
                200, 700, 1400, 2300, 3300, 4400, 5700, 7300, 9000, 11100,
                13400, 16000, 18700, 21600, 24600, 27700, 31100
            };
            int matched = 0;
            for (int i = 0; i < levels.Length; i++)
            {
                long got = Player.XpForLevel(levels[i]);
                if (got == expected[i]) matched++;
                else Console.WriteLine($"FAIL: L{levels[i]} expected {expected[i]} got {got}");
            }
            Console.WriteLine($"[level] public table match = {matched}/17");
            if (matched != levels.Length)
            {
                Console.WriteLine("FAIL: public table mismatch count " + (levels.Length - matched));
                failures++;
            }

            // 6) 单调性：XP 递增时等级不得下降。
            int monotonicBad = 0;
            int previousLevel = 1;
            for (long xp = 0; xp <= 600000; xp += 137)
            {
                int lv = Player.LevelFromXp(xp);
                if (lv < previousLevel) monotonicBad++;
                previousLevel = lv;
            }
            Console.WriteLine($"[level] monotonic violations = {monotonicBad}");
            if (monotonicBad != 0)
            {
                Console.WriteLine("FAIL: level decreased as XP increased");
                failures++;
            }

            // 7) 用户历史存档的 XP 样本（取自 profiles(*.bak) 备份）。
            int[] samples = { 242733, 244796, 248865, 251533, 252508 };
            int[] sampleLevels = { 51, 51, 51, 52, 52 };
            for (int i = 0; i < samples.Length; i++)
            {
                int lv = Player.LevelFromXp(samples[i]);
                Console.WriteLine($"[level] sample xp={samples[i]} -> level={lv}");
                if (lv != sampleLevels[i])
                {
                    Console.WriteLine($"FAIL: sample xp={samples[i]} expected level {sampleLevels[i]}, got {lv}");
                    failures++;
                }
            }

            // 8) 满级门槛与下一级距离。
            long maxLevelXp = Player.MaxLevelXp;
            Console.WriteLine($"[level] maxLevelXp = {maxLevelXp} (expect 575700)");
            if (maxLevelXp != 575700L)
            {
                Console.WriteLine("FAIL: MaxLevelXp should be 575700, got " + maxLevelXp);
                failures++;
            }
            long toNext = Player.XpToNextLevel(realXp);
            Console.WriteLine($"[level] xpToNext({realXp}) = {toNext} (expect 3592)");
            if (toNext != 3592)
            {
                Console.WriteLine("FAIL: XpToNextLevel wrong: " + toNext);
                failures++;
            }
            if (Player.XpToNextLevel(575700) != 0)
            {
                Console.WriteLine("FAIL: XpToNextLevel should be 0 at max level");
                failures++;
            }

            return failures;
        }

        /// <summary>
        /// 等级换算的边界不变量与「改经验 → 等级同步 → 保存 → 重载」闭环。
        /// <para>
        /// 这些断言来自 QA 独立复核（<c>def.scs</c> 权威表逐级比对 + 20 万采样点交叉比对），
        /// 覆盖 <see cref="TestLevelTable"/> 只做抽样、未覆盖到的边界：
        /// 每一级的门槛处判定、每一级的「距下一级」正值、极端 XP 不抛异常、
        /// <see cref="Player.Level"/> setter 往返，以及 ViewModel 的写回闭环。
        /// </para>
        /// </summary>
        private static int TestLevelInvariants()
        {
            int failures = 0;

            // 1) 每一级的门槛处判定：门槛本身 → 该级；门槛 - 1 → 低一级。
            int boundaryBad = 0;
            for (int lv = 1; lv <= Player.MaxLevel; lv++)
            {
                long threshold = Player.XpForLevel(lv);
                if (Player.LevelFromXp(threshold) != lv) boundaryBad++;
                if (lv >= 2 && Player.LevelFromXp(threshold - 1) != lv - 1) boundaryBad++;
            }
            Console.WriteLine($"[levelinv] threshold boundary mismatches = {boundaryBad} (over 100 levels)");
            if (boundaryBad != 0)
            {
                Console.WriteLine("FAIL: level threshold boundary mismatch count " + boundaryBad);
                failures++;
            }

            // 2) 每一级的「距下一级」：恰好在门槛上时必须返回下一级增量（正数）。
            int nextBad = 0;
            for (int lv = 1; lv < Player.MaxLevel; lv++)
            {
                long threshold = Player.XpForLevel(lv);
                long expect = Player.XpForLevel(lv + 1) - threshold;
                long got = Player.XpToNextLevel(threshold);
                if (got != expect || got <= 0) nextBad++;
            }
            Console.WriteLine($"[levelinv] XpToNextLevel mismatches = {nextBad} (over levels 1..99)");
            if (nextBad != 0)
            {
                Console.WriteLine("FAIL: XpToNextLevel mismatch count " + nextBad);
                failures++;
            }

            // 3) 极端输入不得抛异常，且等级必须落在 [1, MaxLevel]。
            long[] edges = { 0, -1, -999999, long.MinValue, long.MaxValue, 575699, 575700, 575701, int.MaxValue };
            int edgeBad = 0;
            foreach (long xp in edges)
            {
                try
                {
                    int lv = Player.LevelFromXp(xp);
                    long next = Player.XpToNextLevel(xp);
                    if (lv < 1 || lv > Player.MaxLevel) edgeBad++;
                    if (next < 0) edgeBad++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FAIL: LevelFromXp({xp}) threw {ex.Message}");
                    edgeBad++;
                }
            }
            Console.WriteLine($"[levelinv] edge input violations = {edgeBad} (over {edges.Length} values)");
            if (edgeBad != 0)
            {
                Console.WriteLine("FAIL: edge input violation count " + edgeBad);
                failures++;
            }

            // 4) XpForLevel 越界：低于 1 级返回 0，高于满级封顶为满级门槛。
            CheckXp(failures, "XpForLevel(int.MinValue)", Player.XpForLevel(int.MinValue), 0);
            CheckXp(failures, "XpForLevel(int.MaxValue)", Player.XpForLevel(int.MaxValue), Player.MaxLevelXp);
            CheckXp(failures, "XpForLevel(MaxLevel+1)", Player.XpForLevel(Player.MaxLevel + 1), Player.MaxLevelXp);
            CheckXp(failures, "XpForLevel(MaxLevel)", Player.XpForLevel(Player.MaxLevel), Player.MaxLevelXp);

            // 5) Player.Level setter 往返：Level → Experience → Level 必须原样还原。
            int setterBad = 0;
            var probe = new SaveFile();
            string dir = Path.Combine(Path.GetTempPath(), "ets2test_levelinv");
            Directory.CreateDirectory(dir);
            string probePath = Path.Combine(dir, "levelinv.sii");
            File.WriteAllText(probePath,
                "SiiNunit\n{\n" +
                "economy : _nameless.0000 {\n experience_points: 0\n}\n" +
                "}\n");
            probe.Load(probePath);
            var probePlayer = probe.GetPlayer();
            if (probePlayer == null)
            {
                Console.WriteLine("FAIL: could not build a Player for the setter round-trip");
                failures++;
            }
            else
            {
                for (int lv = 1; lv <= Player.MaxLevel; lv++)
                {
                    probePlayer.Level = lv;
                    long xpAfter = probePlayer.Experience;
                    int lvBack = probePlayer.Level;
                    if (lvBack != lv || xpAfter != Player.XpForLevel(lv)) setterBad++;
                }
                Console.WriteLine($"[levelinv] Player.Level setter round-trip mismatches = {setterBad} (over 100 levels)");
                if (setterBad != 0)
                {
                    Console.WriteLine("FAIL: Player.Level setter round-trip mismatch count " + setterBad);
                    failures++;
                }
            }

            // 6) 闭环：改经验值 → 等级自动同步 → 写回存档 → 重新加载后等级仍正确。
            if (File.Exists(RealSavePath))
            {
                string vmDir = Path.Combine(Path.GetTempPath(), "ets2test_levelinv");
                Directory.CreateDirectory(vmDir);
                string vmCopy = Path.Combine(vmDir, "levelinv_real_copy.sii");
                File.Copy(RealSavePath, vmCopy, true);   // 只操作副本，绝不碰原存档

                var sf = new SaveFile();
                sf.Load(vmCopy);
                var vm = new ProfileViewModel();
                vm.LoadFrom(sf);

                bool vmOk = vm.Level == 52 && vm.Experience == 252508;
                Console.WriteLine($"[levelinv] viewmodel load: level={vm.Level} xp={vm.Experience} (expect 52 / 252508)");
                if (!vmOk)
                {
                    Console.WriteLine("FAIL: viewmodel did not derive level 52 from the real save");
                    failures++;
                }

                // 改成 60 级对应的 XP，界面应立即同步显示 60 级。
                vm.Experience = Player.XpForLevel(60);
                bool liveOk = vm.Level == 60;
                Console.WriteLine($"[levelinv] viewmodel live sync: xp={vm.Experience} -> level={vm.Level} (expect 60)");
                if (!liveOk)
                {
                    Console.WriteLine("FAIL: viewmodel level did not follow the edited XP");
                    failures++;
                }

                // 写回并保存，然后重新加载，等级必须仍是 60。
                vm.ApplyToModel();
                string outPath = Path.Combine(vmDir, "levelinv_saved.sii");
                sf.Save(outPath, false);

                var sf2 = new SaveFile();
                sf2.Load(outPath);
                var vm2 = new ProfileViewModel();
                vm2.LoadFrom(sf2);
                long savedXp = sf2.GetPlayer()?.Experience ?? -1;
                bool reloadOk = savedXp == Player.XpForLevel(60) && vm2.Level == 60;
                Console.WriteLine($"[levelinv] viewmodel reload: savedXp={savedXp} -> level={vm2.Level} (expect 60)");
                if (!reloadOk)
                {
                    Console.WriteLine("FAIL: level did not survive the save/reload round-trip");
                    failures++;
                }
            }
            else
            {
                Console.WriteLine("[levelinv] SKIP: real save not found, viewmodel round-trip not exercised");
            }

            // 7) 负数经验值不得写进存档（游戏会拒绝加载负值经验）。
            {
                string negDir = Path.Combine(Path.GetTempPath(), "ets2test_levelinv");
                Directory.CreateDirectory(negDir);
                string negPath = Path.Combine(negDir, "levelinv_neg.sii");
                File.WriteAllText(negPath,
                    "SiiNunit\n{\n" +
                    "economy : _nameless.0000 {\n experience_points: 1000\n}\n" +
                    "}\n");

                var negSf = new SaveFile();
                negSf.Load(negPath);
                var negVm = new ProfileViewModel();
                negVm.LoadFrom(negSf);
                negVm.Experience = -5000;          // 模拟用户填了负数
                negVm.ApplyToModel();

                long writtenXp = negSf.GetPlayer()?.Experience ?? -1;
                bool negOk = writtenXp == 0;
                Console.WriteLine($"[levelinv] negative xp clamped: written={writtenXp} (expect 0)");
                if (!negOk)
                {
                    Console.WriteLine("FAIL: negative experience was written to the save: " + writtenXp);
                    failures++;
                }
            }

            return failures;
        }

        private static void CheckLevel(int failures, string label, int actual, int expected)
        {
            Console.WriteLine($"[level] {label} = {actual} (expect {expected})");
            if (actual != expected)
            {
                Console.WriteLine($"FAIL: {label} expected {expected}, got {actual}");
                failures++;
            }
        }

        private static void CheckXp(int failures, string label, long actual, long expected)
        {
            Console.WriteLine($"[level] {label} = {actual} (expect {expected})");
            if (actual != expected)
            {
                Console.WriteLine($"FAIL: {label} expected {expected}, got {actual}");
                failures++;
            }
        }

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

            // 真实存档可能是加密态（ScsC），也可能是本工具保存后的明文态（SiiNunit）——
            // 两种都必须能被正确识别并解析，因此这里只断言「格式已知且可解析」。
            bool formatOk = sf.Format == SiiFormat.Encrypted || sf.Format == SiiFormat.Plaintext;
            long money = sf.GetBank()?.Money ?? -1;
            long xp = sf.GetPlayer()?.Experience ?? -1;
            bool moneyOk = money == 54356889;
            bool xpOk = xp == 252508;

            Console.WriteLine($"[realsave] format={sf.Format} formatOk={formatOk} money={money} xp={xp} units={sf.Document.Units.Count}");
            if (!formatOk) { Console.WriteLine("FAIL: real save format not detected"); failures++; }
            if (!moneyOk) { Console.WriteLine("FAIL: real money_account wrong: " + money); failures++; }
            if (!xpOk) { Console.WriteLine("FAIL: real experience_points wrong: " + xp); failures++; }

            failures += TestRealSaveArrays(sf);

            return failures;
        }

        /// <summary>
        /// 校验真实存档中的数组字段被正确解析。
        /// <para>
        /// 空数组（<c>name: 0</c> 且没有 name[i] 元素行）与普通整数属性在文本
        /// 上无法区分，必须依赖存档自带的结构定义（schema）。若解析错误，
        /// 修理挂车等操作会把 <c>wheels_wear: 0</c> 写成 <c>wheels_wear: 1</c>，
        /// 从而损坏存档。
        /// </para>
        /// </summary>
        private static int TestRealSaveArrays(SaveFile sf)
        {
            int failures = 0;

            var doc = sf.Document;
            if (doc == null)
            {
                Console.WriteLine("FAIL: real save document is null");
                return failures + 1;
            }

            // economy.visited_cities：真实存档中为 332 项。
            var economy = doc.GetFirstByType("economy");
            int visitedCities = economy != null ? economy.GetArrayItems("visited_cities").Count : -1;
            bool visitedOk = visitedCities == 332;
            Console.WriteLine($"[realsave] visited_cities={visitedCities}");
            if (!visitedOk)
            {
                Console.WriteLine("FAIL: visited_cities count wrong: " + visitedCities);
                failures++;
            }

            // garage：非 truckersmp 的车库挂车数组为空（0 项），不是 1 项。
            var garage = doc.GetUnitById("garage.calais");
            int garageTrailers = garage != null ? garage.GetArrayItems("trailers").Count : -1;
            bool garageOk = garageTrailers == 0;
            Console.WriteLine($"[realsave] garage.calais.trailers={garageTrailers}");
            if (!garageOk)
            {
                Console.WriteLine("FAIL: empty garage trailer array wrong: " + garageTrailers);
                failures++;
            }

            // garage：truckersmp 有 6 辆挂车。
            var garage2 = doc.GetUnitById("garage.truckersmp");
            int garage2Trailers = garage2 != null ? garage2.GetArrayItems("trailers").Count : -1;
            bool garage2Ok = garage2Trailers == 6;
            Console.WriteLine($"[realsave] garage.truckersmp.trailers={garage2Trailers}");
            if (!garage2Ok)
            {
                Console.WriteLine("FAIL: garage trailer array wrong: " + garage2Trailers);
                failures++;
            }

            // vehicle.wheels_wear：4 个轮子。
            var vehicles = doc.GetUnitsByType("vehicle");
            int wheelCount = vehicles.Count > 0 ? vehicles[0].GetArrayItems("wheels_wear").Count : -1;
            bool wheelsOk = wheelCount == 4;
            Console.WriteLine($"[realsave] vehicle.wheels_wear={wheelCount}");
            if (!wheelsOk)
            {
                Console.WriteLine("FAIL: vehicle wheel wear array wrong: " + wheelCount);
                failures++;
            }

            // trailer.wheels_wear：真实存档中为空数组（0 项）。
            var trailers = doc.GetUnitsByType("trailer");
            int trailerWheels = trailers.Count > 0 ? trailers[0].GetArrayItems("wheels_wear").Count : -1;
            bool trailerWheelsOk = trailerWheels == 0;
            Console.WriteLine($"[realsave] trailer.wheels_wear={trailerWheels}");
            if (!trailerWheelsOk)
            {
                Console.WriteLine("FAIL: trailer wheel wear array wrong: " + trailerWheels);
                failures++;
            }

            // job_offer_data.trailer_place：空数组。
            var offers = doc.GetUnitsByType("job_offer_data");
            int places = offers.Count > 0 ? offers[0].GetArrayItems("trailer_place").Count : -1;
            bool placesOk = places == 0;
            Console.WriteLine($"[realsave] job_offer_data.trailer_place={places}");
            if (!placesOk)
            {
                Console.WriteLine("FAIL: job offer trailer place array wrong: " + places);
                failures++;
            }

            // 往返必须字节级一致：数组头被正确识别后仍要原样写回。
            // 注意要按存档的实际格式解码——它可能是加密态，也可能是本工具保存后的明文态。
            string original = SiiFormatDetector.Decode(File.ReadAllBytes(RealSavePath), sf.Format);
            string roundtrip = SiiSerializer.Serialize(doc);
            bool exact = original == roundtrip;
            Console.WriteLine($"[realsave] roundtripExact={exact}");
            if (!exact)
            {
                Console.WriteLine("FAIL: real save round-trip is not byte identical");
                failures++;
            }

            return failures;
        }
    }
}
