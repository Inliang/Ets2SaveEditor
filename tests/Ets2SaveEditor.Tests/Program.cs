using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;
using SaveCompany = Ets2SaveEditor.Core.Domain.TypedUnits.Company;
using Ets2SaveEditor.Core.GameData;
using Ets2SaveEditor.Core.Persistence;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.Core.Sii.Model;
using Ets2SaveEditor.ViewModels;

namespace Ets2SaveEditor.Tests
{
    public static class Program
    {
        /// <summary>
        /// Real ETS2 save used for the optional integration test. The file is
        /// only ever read and copied to a temporary directory — it is never
        /// modified or overwritten.
        /// <para>
        /// 这是仓库里**唯一**允许出现的真实存档绝对路径（只读基准）。[nosavepath]
        /// 断言会单独检查它，并要求它只被只读使用。
        /// </para>
        /// </summary>
        private static readonly string RealSavePath = @"C:\Users\Administrator\Documents\Euro Truck Simulator 2\profiles\4361726F6C696E65E4B8B6\save\1\game.sii";

        /// <summary>
        /// Root directory for all temporary test files.
        /// <para>
        /// The directory name contains the process id so that several test
        /// processes can run at the same time (for example a developer running
        /// the suite while CI or a review agent runs it in parallel) without
        /// overwriting each other's fixtures. Without this isolation the
        /// assertions became flaky: one process truncated a file another
        /// process was reading.
        /// </para>
        /// </summary>
        private static readonly string TempRoot = Path.Combine(
            Path.GetTempPath(),
            "ets2test_" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Creates (and returns) a private temporary directory for the current
        /// test process. The directory is emptied first so each test starts
        /// from a known state.
        /// </summary>
        /// <param name="name">Sub-directory name, unique within the process.</param>
        /// <returns>The absolute path of the created directory.</returns>
        private static string CreateTempDir(string name)
        {
            string dir = Path.Combine(TempRoot, name);
            // 先清空再建目录：直接 CreateDirectory 不会清掉上一轮遗留的文件，
            // 而先删后建必须保证目录最终存在，否则后续写文件会因路径缺失而失败。
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        }

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
            failures += TestSingleVehicleActions();
            failures += TestGameNames();
            failures += TestGarageOwnership();
            failures += TestDocumentIndex();
            failures += TestCompanyFilter();
            failures += TestCargoSelection();
            failures += TestTrailerModelName();
            failures += TestBuiltInGameData();
            failures += TestCargoNames();
            failures += TestDocNumbers();
            failures += TestUiMenuEnable();
            failures += TestNoRealSavePath();
            failures += TestDisplayName();
            failures += TestNullReference();
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

            string dir = CreateTempDir("root");
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

            // 浮点必须写成游戏的十六进制形式（&xxxxxxxx），不能出现十进制小数。
            // 注意只判断「": 0.7"」这种带值前缀的形态：新建任务的随机 id
            // （_nameless.1b0.7c41.5d68）里也可能出现 "0.7"，直接全文匹配
            // 会让这条断言随机失败。
            bool hexOk = !saved.Contains(": 0.7") && !saved.Contains("wear : ");
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

            string dir = CreateTempDir("root");
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

            string dir = CreateTempDir("root");
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
            string dir = CreateTempDir("root");
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

            string dir = CreateTempDir("backup_uniqueness");

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

            string dir = CreateTempDir("atomic_save");

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

            string dir = CreateTempDir("style");

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

            string dir = CreateTempDir("root");
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

            string dir = CreateTempDir("root");
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
            string dir = CreateTempDir("levelinv");
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
                string vmDir = CreateTempDir("levelinv");
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

                // 8) 直接编辑「目标等级」→ 应换算成该等级的累计经验值，并可存回存档。
                vm2.Level = 75;
                bool levelEditOk = vm2.Experience == Player.XpForLevel(75) && vm2.Level == 75;
                Console.WriteLine($"[levelinv] target level edit: level=75 -> xp={vm2.Experience} (expect {Player.XpForLevel(75)})");
                if (!levelEditOk)
                {
                    Console.WriteLine("FAIL: editing the target level did not convert to the matching XP");
                    failures++;
                }

                // 9) 越界的「目标等级」：小于 1 应被忽略（不清零进度），大于满级应封顶。
                vm2.Level = 0;
                bool lowLevelOk = vm2.Experience == Player.XpForLevel(75) && vm2.Level == 75;
                Console.WriteLine($"[levelinv] target level 0 ignored: xp={vm2.Experience} level={vm2.Level} (expect unchanged 75)");
                if (!lowLevelOk)
                {
                    Console.WriteLine("FAIL: target level 0 should be ignored, not reset the progress");
                    failures++;
                }

                vm2.Level = Player.MaxLevel + 10;
                bool highLevelOk = vm2.Experience == Player.MaxLevelXp && vm2.Level == Player.MaxLevel;
                Console.WriteLine($"[levelinv] target level overflow capped: xp={vm2.Experience} level={vm2.Level} (expect {Player.MaxLevel})");
                if (!highLevelOk)
                {
                    Console.WriteLine("FAIL: target level above max should be capped");
                    failures++;
                }
            }
            else
            {
                Console.WriteLine("[levelinv] SKIP: real save not found, viewmodel round-trip not exercised");
            }

            // 7) 负数经验值不得写进存档（游戏会拒绝加载负值经验）。
            {
                string negDir = CreateTempDir("levelinv");
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

        /// <summary>
        /// 校验名称解析、车库归属判定与「只看车库」过滤。
        /// <para>
        /// 这些数据都来自真实存档：游戏里存的全是 token，显示名在游戏自己的
        /// 数据文件里（由 tools/extract_names.py 提取到 GameNames 表）。
        /// </para>
        /// </summary>
        /// <summary>
        /// 校验「只修理/加满某一辆」的单项操作：只影响选中的那一个单元，
        /// 其它车辆不受影响，且修改能写回存档。
        /// </summary>
        private static int TestSingleVehicleActions()
        {
            int failures = 0;

            string sample =
                "SiiNunit\n{\n" +
                "economy : _nameless.0000 {\n experience_points: 1000\n}\n" +
                "bank : _nameless.0001 {\n money_account: 100\n}\n" +
                "vehicle : _nameless.0002 {\n" +
                " engine_wear: &3f000000\n" +
                " transmission_wear: &3f000000\n" +
                " cabin_wear: &3f000000\n" +
                " chassis_wear: &3f000000\n" +
                " wheels_wear: 1\n" +
                " wheels_wear[0]: &3f000000\n" +
                " fuel_relative: &3ecccccd\n" +
                " license_plate: \"AA 111AA\"\n" +
                "}\n" +
                "vehicle : _nameless.0003 {\n" +
                " engine_wear: &3f000000\n" +
                " transmission_wear: &3f000000\n" +
                " cabin_wear: &3f000000\n" +
                " chassis_wear: &3f000000\n" +
                " wheels_wear: 1\n" +
                " wheels_wear[0]: &3f000000\n" +
                " fuel_relative: &3ecccccd\n" +
                " license_plate: \"BB 222BB\"\n" +
                "}\n" +
                "}\n";

            string dir = CreateTempDir("single");
            string inPath = Path.Combine(dir, "single.sii");
            File.WriteAllText(inPath, sample);

            var sf = new SaveFile();
            sf.Load(inPath);

            var truck = new TruckViewModel();
            truck.LoadFrom(sf);
            if (truck.Items.Count != 2)
            {
                Console.WriteLine("FAIL: expected 2 vehicles, got " + truck.Items.Count);
                return failures + 1;
            }

            // 只修理第一辆
            truck.RepairOneCommand.Execute(truck.Items[0]);
            // 只给第二辆加满油
            truck.RefuelOneCommand.Execute(truck.Items[1]);

            double firstEngine = truck.Items[0].EngineWear;
            double secondEngine = truck.Items[1].EngineWear;
            double firstFuel = truck.Items[0].Fuel;
            double secondFuel = truck.Items[1].Fuel;

            Console.WriteLine($"[single] after: #0 engine={firstEngine:P0} fuel={firstFuel:P0}  " +
                            $"#1 engine={secondEngine:P0} fuel={secondFuel:P0}");

            bool repairOk = firstEngine == 0;
            bool untouchedOk = secondEngine > 0;
            bool refuelOk = secondFuel == 1.0;
            bool fuelUntouchedOk = firstFuel < 1.0;

            if (!repairOk) { Console.WriteLine("FAIL: single repair did not clear wear"); failures++; }
            if (!untouchedOk) { Console.WriteLine("FAIL: single repair touched the other vehicle"); failures++; }
            if (!refuelOk) { Console.WriteLine("FAIL: single refuel did not fill the tank"); failures++; }
            if (!fuelUntouchedOk) { Console.WriteLine("FAIL: single refuel refilled the other vehicle"); failures++; }

            // 写回存档后重新加载，修改应已持久化
            truck.ApplyToModel();
            string outPath = Path.Combine(dir, "single.out.sii");
            sf.Save(outPath, false);

            var sf2 = new SaveFile();
            sf2.Load(outPath);
            var truck2 = new TruckViewModel();
            truck2.LoadFrom(sf2);

            bool persisted = truck2.Items.Count == 2 &&
                             truck2.Items[0].EngineWear == 0 &&
                             truck2.Items[1].Fuel == 1.0 &&
                             truck2.Items[1].EngineWear > 0;
            Console.WriteLine($"[single] persisted: #0 engine={truck2.Items[0].EngineWear:P0} " +
                              $"#1 fuel={truck2.Items[1].Fuel:P0} #1 engine={truck2.Items[1].EngineWear:P0}");
            if (!persisted)
            {
                Console.WriteLine("FAIL: single vehicle changes not persisted");
                failures++;
            }

            return failures;
        }

        private static int TestGameNames()
        {
            int failures = 0;

            // 1) 货物名：token -> 可读名称
            string cargoName = CargoCatalogue.DisplayName("cargo.gummy_bears");
            bool cargoOk = cargoName == "Gummy Bears";
            Console.WriteLine($"[names] cargo.gummy_bears -> '{cargoName}' (expect Gummy Bears)");
            if (!cargoOk)
            {
                Console.WriteLine("FAIL: cargo display name wrong: " + cargoName);
                failures++;
            }

            // 未收录的货物应回退到可读的 token 形式，而不是空串
            string fallback = CargoCatalogue.DisplayName("cargo.not_a_real_cargo");
            bool fallbackOk = !string.IsNullOrEmpty(fallback) &&
                              fallback.IndexOf("not a real cargo", StringComparison.OrdinalIgnoreCase) >= 0;
            Console.WriteLine($"[names] unknown cargo -> '{fallback}'");
            if (!fallbackOk)
            {
                Console.WriteLine("FAIL: unknown cargo should fall back to a readable token");
                failures++;
            }

            // 2) 品牌名：company.permanent.<brand>
            string brand = GameNames.Lookup(GameNames.Company, "company.permanent.acc");
            bool brandOk = brand == "ACC";
            Console.WriteLine($"[names] company.permanent.acc -> '{brand}' (expect ACC)");
            if (!brandOk)
            {
                Console.WriteLine("FAIL: company display name wrong: " + brand);
                failures++;
            }

            // 3) 名称表覆盖度
            bool cargoTableOk = GameNames.Cargo.Count >= 300;
            bool companyTableOk = GameNames.Company.Count >= 100;
            Console.WriteLine($"[names] table sizes: cargo={GameNames.Cargo.Count} company={GameNames.Company.Count}");
            if (!cargoTableOk) { Console.WriteLine("FAIL: cargo name table too small"); failures++; }
            if (!companyTableOk) { Console.WriteLine("FAIL: company name table too small"); failures++; }

            // 4) 显示时不应露出本地化占位符（表里保留原文，便于以后补全）
            int placeholder = 0;
            foreach (var kv in GameNames.Cargo)
                if (!string.IsNullOrEmpty(GameNames.Lookup(GameNames.Cargo, kv.Key)) &&
                    kv.Value.StartsWith("@@", StringComparison.Ordinal)) placeholder++;
            foreach (var kv in GameNames.Company)
                if (!string.IsNullOrEmpty(GameNames.Lookup(GameNames.Company, kv.Key)) &&
                    kv.Value.StartsWith("@@", StringComparison.Ordinal)) placeholder++;
            Console.WriteLine($"[names] placeholders leaking into display = {placeholder}");
            if (placeholder != 0)
            {
                Console.WriteLine("FAIL: some display names are still localization placeholders");
                failures++;
            }

            // 5) 只有 DLC 才提供的货物（本地化条目缺失）应回退成可读 token
            string dlcOnly = CargoCatalogue.DisplayName("cargo.obsole.waste_cont");
            bool dlcOk = !string.IsNullOrEmpty(dlcOnly) &&
                          dlcOnly.IndexOf("@@", StringComparison.Ordinal) < 0;
            Console.WriteLine($"[names] DLC-only cargo -> '{dlcOnly}'");
            if (!dlcOk)
            {
                Console.WriteLine("FAIL: DLC-only cargo should fall back, not show a placeholder");
                failures++;
            }

            return failures;
        }

        /// <summary>
        /// 校验车库归属判定：存档里会保留玩家卖掉的车/挂车对应的单元，
        /// 只有还停在车库车位上的才算拥有。
        /// </summary>
        private static int TestGarageOwnership()
        {
            int failures = 0;
            if (!File.Exists(RealSavePath))
            {
                Console.WriteLine("[ownership] SKIP: real save not found");
                return 0;
            }

            string dir = CreateTempDir("ownership");
            string copy = Path.Combine(dir, "ownership_copy.sii");
            File.Copy(RealSavePath, copy, true);   // 只操作副本

            var sf = new SaveFile();
            sf.Load(copy);

            var vehicles = sf.GetVehicles();
            var trailers = sf.GetTrailers();

            int ownedVehicles = 0;
            foreach (var v in vehicles) if (v.IsInGarage) ownedVehicles++;
            int ownedTrailers = 0;
            foreach (var t in trailers) if (t.IsInGarage) ownedTrailers++;

            Console.WriteLine($"[ownership] vehicles total={vehicles.Count} inGarage={ownedVehicles}");
            Console.WriteLine($"[ownership] trailers total={trailers.Count} inGarage={ownedTrailers}");

            if (ownedVehicles <= 0 || ownedVehicles >= vehicles.Count)
            {
                Console.WriteLine("FAIL: vehicle ownership split looks wrong");
                failures++;
            }
            if (ownedTrailers <= 0 || ownedTrailers >= trailers.Count)
            {
                Console.WriteLine("FAIL: trailer ownership split looks wrong");
                failures++;
            }

            // 车库计数必须与实际停放的车辆数一致（数组里的 null 占位要排除）
            int garageSum = 0;
            foreach (var g in sf.GetGarages()) garageSum += g.VehicleCount;
            Console.WriteLine($"[ownership] garage vehicle count sum = {garageSum}");
            if (garageSum != ownedVehicles)
            {
                Console.WriteLine($"FAIL: garage counts ({garageSum}) != vehicles in garages ({ownedVehicles})");
                failures++;
            }

            // 「只看车库」过滤：ViewModel 打开开关后列表应只剩拥有的车
            var truckVm = new TruckViewModel();
            truckVm.LoadFrom(sf);
            int allCount = truckVm.Items.Count;
            truckVm.OnlyInGarage = true;
            int ownedCount = truckVm.Items.Count;
            Console.WriteLine($"[ownership] viewmodel all={allCount} onlyInGarage={ownedCount}");
            if (ownedCount != ownedVehicles)
            {
                Console.WriteLine($"FAIL: onlyInGarage filter count {ownedCount} != {ownedVehicles}");
                failures++;
            }

            return failures;
        }

        private static int TestRealSave()
        {
            if (!File.Exists(RealSavePath))
            {
                Console.WriteLine("[realsave] SKIP: real save not found");
                return 0;
            }

            int failures = 0;
            string dir = CreateTempDir("root");
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

        // ------------------------------------------------------------------
        // 文档查找索引测试。
        //
        // 存档查找从「每次都线性扫描全部单元」改成了索引，结果必须与线性
        // 扫描完全一致：同样的 id 命中同样的单元、同样的类型返回同样的单元
        // 集合（顺序也一致）。增删单元后索引也必须立刻跟上。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验文档查找的索引实现：与大文档的线性扫描逐项比对、增删单元后
        /// 索引仍然正确，以及车辆/挂车所属车库的反向索引。
        /// </summary>
        private static int TestDocumentIndex()
        {
            int failures = 0;

            // 造一个大文档：多种类型交替出现，最后还有一个重复 id。
            var text = new StringBuilder();
            text.Append("SiiNunit\n{\n");
            const int unitCount = 2000;
            for (int i = 0; i < unitCount; i++)
            {
                string type = i % 3 == 0 ? "vehicle" : (i % 3 == 1 ? "garage" : "trailer");
                text.Append(type + " : unit." + i + " {\n some_value: " + i + "\n}\n");
            }
            // 重复 id：线性扫描返回先出现的那一个。
            text.Append("vehicle : unit.0 {\n some_value: 999\n}\n");
            text.Append("}\n");

            SiiDocument doc = SiiParser.Parse(text.ToString());

            // 1) 按 id 查找：与线性扫描逐个比对。
            int idMismatch = 0;
            for (int i = 0; i < unitCount; i++)
            {
                string id = "unit." + i;
                if (!ReferenceEquals(LinearFindById(doc, id), doc.GetUnitById(id))) idMismatch++;
            }
            Console.WriteLine($"[index] id lookup mismatches vs linear scan = {idMismatch} (over {unitCount} ids)");
            if (idMismatch != 0)
            {
                Console.WriteLine("FAIL: indexed id lookup differs from the linear scan");
                failures++;
            }

            // 2) 按类型查找：集合内容与顺序都要一致。
            //    遍历**文档里出现过的全部类型**（而不是抽查几种）—— 只查少数几种，
            //    其余类型的索引条目一旦出错就不会被发现。
            int typeMismatch = 0;
            var docTypes = AllTypesOf(doc);
            docTypes.Add("not_a_type");     // 不存在的类型必须返回空集合
            foreach (string type in docTypes)
            {
                List<SiiUnit> expected = LinearByType(doc, type);
                List<SiiUnit> actual = doc.GetUnitsByType(type);
                if (expected.Count != actual.Count) { typeMismatch++; continue; }
                for (int i = 0; i < expected.Count; i++)
                {
                    if (!ReferenceEquals(expected[i], actual[i])) { typeMismatch++; break; }
                }
            }
            Console.WriteLine($"[index] type lookup mismatches vs linear scan = {typeMismatch} (over {docTypes.Count} types)");
            if (typeMismatch != 0)
            {
                Console.WriteLine("FAIL: indexed type lookup differs from the linear scan");
                failures++;
            }

            // 3) GetFirstByType 必须返回文档里第一个该类型的单元。
            SiiUnit firstVehicle = doc.GetFirstByType("vehicle");
            bool firstOk = firstVehicle != null && firstVehicle.Id == "unit.0" &&
                           doc.GetFirstByType("not_a_type") == null &&
                           doc.GetFirstByType("") == null;
            Console.WriteLine($"[index] first-by-type = {firstVehicle?.Id ?? "(null)"}");
            if (!firstOk)
            {
                Console.WriteLine("FAIL: GetFirstByType does not return the first unit of the type");
                failures++;
            }

            // 4) 空参数的行为与线性扫描一致：找不到就是 null / 空列表。
            bool emptyOk = doc.GetUnitById(null) == null && doc.GetUnitById("") == null &&
                           doc.GetUnitById("no.such.unit") == null &&
                           doc.GetUnitsByType("").Count == 0 &&
                           doc.GetUnitsByType(null).Count == 0;
            if (!emptyOk)
            {
                Console.WriteLine("FAIL: empty lookups do not behave like the linear scan");
                failures++;
            }

            // 5) 通过 AddUnit / RemoveUnit 增删单元后，索引必须立刻正确。
            int vehiclesBefore = doc.GetUnitsByType("vehicle").Count;
            var added = new SiiUnit("vehicle", "unit.added");
            doc.AddUnit(added);
            bool addOk = ReferenceEquals(doc.GetUnitById("unit.added"), added) &&
                         doc.GetUnitsByType("vehicle").Count == vehiclesBefore + 1;
            doc.RemoveUnit(added);
            bool removeOk = doc.GetUnitById("unit.added") == null &&
                            doc.GetUnitsByType("vehicle").Count == vehiclesBefore;
            Console.WriteLine($"[index] add/remove unit keeps index in sync: add={addOk} remove={removeOk}");
            if (!addOk || !removeOk)
            {
                Console.WriteLine("FAIL: AddUnit / RemoveUnit did not keep the index in sync");
                failures++;
            }

            // 6) 外部直接改列表时，数量对不上会自动重建索引。
            doc.Units.Add(new SiiUnit("vehicle", "unit.direct"));
            bool directOk = doc.GetUnitById("unit.direct") != null;
            if (!directOk)
            {
                Console.WriteLine("FAIL: a directly added unit was not found");
                failures++;
            }

            // 7) 数量没变（删一个加一个）时索引无法自动发现，显式失效后必须正确。
            int removedIndex = doc.Units.FindIndex(u => u != null && u.Id == "unit." + (unitCount - 1));
            doc.Units.RemoveAt(removedIndex);
            doc.Units.Add(new SiiUnit("vehicle", "unit.replaced"));
            doc.InvalidateIndex();
            bool replacedOk = doc.GetUnitById("unit.replaced") != null &&
                              doc.GetUnitById("unit." + (unitCount - 1)) == null;
            Console.WriteLine($"[index] invalidate after in-place replacement: {replacedOk}");
            if (!replacedOk)
            {
                Console.WriteLine("FAIL: InvalidateIndex did not rebuild the index");
                failures++;
            }

            // 8) 车库归属的反向索引。
            failures += TestGarageOwnerIndex();

            // 9) 真实存档（16000+ 单元）上再比一次，确保规模没问题。
            failures += TestRealSaveIndex();

            return failures;
        }

        /// <summary>
        /// 线性扫描的参考实现：索引改造前的行为就是这样的，测试用它逐项比对。
        /// </summary>
        private static SiiUnit? LinearFindById(SiiDocument doc, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var u in doc.Units)
            {
                if (u != null && u.Id == id) return u;
            }
            return null;
        }

        /// <summary>线性扫描的参考实现：按类型收集单元（保持文档顺序）。</summary>
        private static List<SiiUnit> LinearByType(SiiDocument doc, string type)
        {
            var result = new List<SiiUnit>();
            if (string.IsNullOrEmpty(type)) return result;
            foreach (var u in doc.Units)
            {
                if (u != null && u.Type == type) result.Add(u);
            }
            return result;
        }

        /// <summary>
        /// 校验「车辆/挂车 → 所属车库」的反向索引：还在车库车位上的才算拥有，
        /// 卖掉的那些单元必须返回空串。
        /// </summary>
        private static int TestGarageOwnerIndex()
        {
            int failures = 0;

            string sample =
                "SiiNunit\n{\n" +
                "garage : garage.calais {\n vehicles: 2\n vehicles[0]: unit.0\n vehicles[1]: null\n" +
                " trailers: 1\n trailers[0]: unit.2\n}\n" +
                "garage : garage.truckersmp {\n vehicles: 1\n vehicles[0]: unit.3\n trailers: 0\n}\n" +
                "vehicle : unit.0 {\n}\n" +
                "vehicle : unit.1 {\n}\n" +
                "trailer : unit.2 {\n}\n" +
                "}\n";

            SiiDocument doc = SiiParser.Parse(sample);

            bool indexOk = doc.FindGarageOwner("unit.0") == "garage.calais" &&
                           doc.FindGarageOwner("unit.2") == "garage.calais" &&
                           doc.FindGarageOwner("unit.3") == "garage.truckersmp" &&
                           doc.FindGarageOwner("unit.1") == "" &&
                           doc.FindGarageOwner("") == "" &&
                           doc.FindGarageOwner(null) == "" &&
                           doc.FindGarageOwner("null") == "";
            Console.WriteLine($"[index] garage owner index ok = {indexOk}");
            if (!indexOk)
            {
                Console.WriteLine("FAIL: garage owner index returned the wrong garage");
                failures++;
            }

            // 与车辆/挂车包装类的判定必须一致。
            var parked = new Vehicle(doc.GetUnitById("unit.0"), doc);
            var sold = new Vehicle(doc.GetUnitById("unit.1"), doc);
            var trailer = new Trailer(doc.GetUnitById("unit.2"), doc);
            bool wrapperOk = parked.GarageId == "garage.calais" && parked.IsInGarage &&
                             sold.GarageId == "" && !sold.IsInGarage &&
                             trailer.GarageId == "garage.calais" && trailer.IsInGarage;
            Console.WriteLine($"[index] vehicle/trailer garage id ok = {wrapperOk}");
            if (!wrapperOk)
            {
                Console.WriteLine("FAIL: Vehicle/Trailer garage id disagrees with the index");
                failures++;
            }

            return failures;
        }

        /// <summary>
        /// 在真实存档上比对索引与线性扫描的结果（真实存档有一万多个单元）。
        /// </summary>
        private static int TestRealSaveIndex()
        {
            if (!File.Exists(RealSavePath))
            {
                Console.WriteLine("[index] SKIP: real save not found");
                return 0;
            }

            int failures = 0;
            string dir = CreateTempDir("index");
            string copy = Path.Combine(dir, "index_copy.sii");
            File.Copy(RealSavePath, copy, true);   // 只操作副本

            var sf = new SaveFile();
            sf.Load(copy);
            SiiDocument doc = sf.Document;

            // 1) 全部单元的 id 都要能按 id 找回来。
            int mismatch = 0;
            int checkedIds = 0;
            foreach (var unit in doc.Units)
            {
                if (unit == null) continue;
                checkedIds++;
                if (!ReferenceEquals(unit, doc.GetUnitById(unit.Id))) mismatch++;
            }
            Console.WriteLine($"[index] real save: id mismatches = {mismatch} (over {checkedIds} units)");
            if (mismatch != 0)
            {
                Console.WriteLine("FAIL: real save id lookup mismatch count " + mismatch);
                failures++;
            }

            // 2) 全部类型的结果都要与线性扫描一致（真实存档里有 47 种类型）。
            int typeMismatch = 0;
            var saveTypes = AllTypesOf(doc);
            foreach (string type in saveTypes)
            {
                List<SiiUnit> expected = LinearByType(doc, type);
                List<SiiUnit> actual = doc.GetUnitsByType(type);
                if (expected.Count != actual.Count) { typeMismatch++; continue; }
                for (int i = 0; i < expected.Count; i++)
                {
                    if (!ReferenceEquals(expected[i], actual[i])) { typeMismatch++; break; }
                }
            }
            Console.WriteLine($"[index] real save: type mismatches = {typeMismatch} (over {saveTypes.Count} types)");
            if (typeMismatch != 0)
            {
                Console.WriteLine("FAIL: real save type lookup mismatch count " + typeMismatch);
                failures++;
            }

            // 3) 车辆所属车库：索引与逐个车库扫描的结果必须一致。
            int garageMismatch = 0;
            foreach (var vehicle in sf.GetVehicles())
            {
                string expected = LinearGarageOf(doc, vehicle.Unit.Id, "vehicles");
                if (!string.Equals(expected, vehicle.GarageId, StringComparison.Ordinal)) garageMismatch++;
            }
            foreach (var trailer in sf.GetTrailers())
            {
                string expected = LinearGarageOf(doc, trailer.Unit.Id, "trailers");
                if (!string.Equals(expected, trailer.GarageId, StringComparison.Ordinal)) garageMismatch++;
            }
            Console.WriteLine($"[index] real save: garage owner mismatches = {garageMismatch}");
            if (garageMismatch != 0)
            {
                Console.WriteLine("FAIL: real save garage owner mismatch count " + garageMismatch);
                failures++;
            }

            return failures;
        }

        /// <summary>
        /// 线性扫描车库归属的参考实现（索引改造前的行为）。
        /// </summary>
        private static string LinearGarageOf(SiiDocument doc, string unitId, string arrayName)
        {
            foreach (var garage in doc.GetUnitsByType("garage"))
            {
                foreach (var item in garage.GetArrayItems(arrayName))
                {
                    if (string.Equals(item.AsId(), unitId, StringComparison.Ordinal)) return garage.Id;
                }
            }
            return string.Empty;
        }

        // ------------------------------------------------------------------
        // 货运市场：公司筛选与货物下拉框。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验公司筛选：界面上显示的是「公司名 · 城市」，所以筛选必须同时
        /// 匹配显示名、品牌名、公司 id 与城市名，且忽略大小写。
        /// </summary>
        private static int TestCompanyFilter()
        {
            int failures = 0;

            string dir = CreateTempDir("filter");
            string path = Path.Combine(dir, "filter.sii");
            File.WriteAllText(path, FreightSample);

            var sf = new SaveFile();
            sf.Load(path);

            var freight = new FreightMarketViewModel();
            freight.LoadFrom(sf);

            if (freight.Companies.Count != 2)
            {
                Console.WriteLine("FAIL: expected 2 companies, got " + freight.Companies.Count);
                failures++;
            }

            // 显示名（界面上显示的就是它）。
            CheckFilter(failures, freight, "NBFC", 1);
            // 显示名的大小写变体。
            CheckFilter(failures, freight, "nbfc", 1);
            CheckFilter(failures, freight, "NbFc", 1);
            // 品牌名。
            CheckFilter(failures, freight, "stokes", 1);
            // 公司 id。
            CheckFilter(failures, freight, "company.volatile.nbfc.aberdeen", 1);
            CheckFilter(failures, freight, "company.volatile", 2);
            // 城市名（中文）。
            CheckFilter(failures, freight, "阿伯丁", 1);
            CheckFilter(failures, freight, "爱尔福特", 1);
            // 界面上显示的完整文字。
            CheckFilter(failures, freight, "NBFC · 阿伯丁", 1);
            // 查不到的东西。
            CheckFilter(failures, freight, "不存在的公司", 0);
            // 空筛选词：不过滤。
            CheckFilter(failures, freight, "", 2);

            // 刷新后必须保留用户选中的公司（公司列表每次都是新的包装对象，
            // 只能按 id 匹配，否则每次刷新都被重置成第一项）。
            freight.CompanyFilter = "";
            var nbfc = freight.Companies.FirstOrDefault(c => c.Id == "company.volatile.nbfc.aberdeen");
            freight.SelectedCompany = nbfc;
            freight.CompanyFilter = "nbfc";   // 筛选后仍然包含选中的公司
            bool keptOk = freight.SelectedCompany != null &&
                          freight.SelectedCompany.Id == "company.volatile.nbfc.aberdeen";
            Console.WriteLine($"[filter] selected company kept after filter change = {keptOk}");
            if (!keptOk)
            {
                Console.WriteLine("FAIL: the selected company was reset by a refresh");
                failures++;
            }

            // 选中的公司被筛掉了：退回筛选结果里的第一项。
            freight.CompanyFilter = "stokes";
            bool fallbackOk = freight.SelectedCompany != null &&
                              freight.SelectedCompany.Id == "company.volatile.stokes.erfurt";
            Console.WriteLine($"[filter] selected company falls back when filtered out = {fallbackOk}");
            if (!fallbackOk)
            {
                Console.WriteLine("FAIL: the selected company was not adjusted after filtering it out");
                failures++;
            }

            return failures;
        }

        /// <summary>设置筛选词并断言命中数量。</summary>
        private static void CheckFilter(int failures, FreightMarketViewModel vm, string filter, int expected)
        {
            vm.CompanyFilter = filter;
            int actual = vm.Companies.Count;
            Console.WriteLine($"[filter] '{filter}' -> {actual} (expect {expected})");
            if (actual != expected)
            {
                Console.WriteLine($"FAIL: filter '{filter}' matched {actual} companies, expected {expected}");
                failures++;
            }
        }

        /// <summary>
        /// 校验新建任务表单里的货物选择：下拉框与货物 token 双向同步，
        /// 刷新（改公司筛选词也会触发刷新）后必须保留用户已选的货物。
        /// </summary>
        private static int TestCargoSelection()
        {
            int failures = 0;

            string dir = CreateTempDir("cargo");
            string path = Path.Combine(dir, "cargo.sii");
            File.WriteAllText(path, FreightSample);

            var sf = new SaveFile();
            sf.Load(path);

            var freight = new FreightMarketViewModel();
            freight.LoadFrom(sf);

            // 1) 载入后下拉框与默认货物一致。
            bool defaultOk = freight.SelectedCargo != null &&
                             freight.SelectedCargo.Token == freight.NewCargo;
            Console.WriteLine($"[cargo] default selection ok = {defaultOk} (token={freight.NewCargo})");
            if (!defaultOk)
            {
                Console.WriteLine("FAIL: the cargo picker did not select the default cargo");
                failures++;
            }

            // 2) 下拉框选中项 → 货物 token。
            var gummy = freight.CargoChoices.FirstOrDefault(c => c.Token == "cargo.gummy_bears");
            if (gummy == null)
            {
                Console.WriteLine("FAIL: cargo.gummy_bears is missing from the picker");
                failures++;
            }
            else
            {
                freight.SelectedCargo = gummy;
                bool downOk = freight.NewCargo == "cargo.gummy_bears";
                Console.WriteLine($"[cargo] picker -> token: {freight.NewCargo}");
                if (!downOk)
                {
                    Console.WriteLine("FAIL: picking a cargo did not update the cargo token");
                    failures++;
                }
            }

            // 3) 刷新（改筛选词也会触发刷新）后必须保留用户的选择。
            freight.CompanyFilter = "stokes";
            bool keptOk = freight.SelectedCargo != null &&
                          freight.SelectedCargo.Token == "cargo.gummy_bears" &&
                          freight.NewCargo == "cargo.gummy_bears";
            Console.WriteLine($"[cargo] selection kept after filter change = {keptOk}");
            if (!keptOk)
            {
                Console.WriteLine("FAIL: the cargo selection was reset by a refresh");
                failures++;
            }

            freight.CompanyFilter = "";
            bool keptAfterClear = freight.SelectedCargo != null &&
                                  freight.SelectedCargo.Token == "cargo.gummy_bears";
            if (!keptAfterClear)
            {
                Console.WriteLine("FAIL: the cargo selection was reset when the filter was cleared");
                failures++;
            }

            // 4) 货物 token → 下拉框选中项（反向同步）。
            freight.NewCargo = "cargo.milk";
            bool upOk = freight.SelectedCargo != null &&
                        freight.SelectedCargo.Token == "cargo.milk";
            Console.WriteLine($"[cargo] token -> picker ok = {upOk}");
            if (!upOk)
            {
                Console.WriteLine("FAIL: setting the cargo token did not update the picker");
                failures++;
            }

            // 5) 不存在的 token：选中项清空，但不能抛异常。
            freight.NewCargo = "cargo.does_not_exist";
            bool clearedOk = freight.SelectedCargo == null;
            Console.WriteLine($"[cargo] unknown token clears the selection = {clearedOk}");
            if (!clearedOk)
            {
                Console.WriteLine("FAIL: an unknown cargo token did not clear the selection");
                failures++;
            }

            // 6) 没选货物时创建任务：必须回落到默认货物，不能写出空引用。
            freight.NewCargo = string.Empty;
            freight.SelectedCompany = freight.Companies.FirstOrDefault();
            var company = freight.SelectedCompany;
            int offersBefore = company?.JobOfferCount ?? 0;
            int jobsBefore = sf.GetFreightJobs().Count;

            freight.CreateJobCommand.Execute(null);

            var jobs = sf.GetFreightJobs();
            var created = jobs.Count > jobsBefore ? jobs[jobs.Count - 1] : null;
            bool createdOk = created != null &&
                            created.Cargo == "cargo.milk" &&
                            company != null &&
                            company.JobOfferCount == offersBefore + 1;
            Console.WriteLine($"[cargo] created job cargo = {created?.Cargo ?? "(none)"} " +
                              $"(company offers {offersBefore} -> {company?.JobOfferCount})");
            if (!createdOk)
            {
                Console.WriteLine("FAIL: creating a job without a cargo selection did not fall back to the default");
                failures++;
            }

            // 7) 用户明确指定的货物 token 必须原样写进存档。
            //    早期实现会因为它「不在下拉框目录里」就悄悄换成默认货物，
            //    于是存档里出现与用户意图不符的货（DLC 货物 cargo.halloween
            //    就曾被改成 cargo.haulie）。
            freight.NewCargo = "cargo.halloween";
            int jobsBeforeExplicit = sf.GetFreightJobs().Count;
            freight.CreateJobCommand.Execute(null);

            var jobsExplicit = sf.GetFreightJobs();
            var createdExplicit = jobsExplicit.Count > jobsBeforeExplicit
                ? jobsExplicit[jobsExplicit.Count - 1]
                : null;
            bool explicitOk = createdExplicit != null && createdExplicit.Cargo == "cargo.halloween";
            Console.WriteLine($"[cargo] explicitly chosen cargo kept = {explicitOk} " +
                              $"(cargo={createdExplicit?.Cargo ?? "(none)"})");
            if (!explicitOk)
            {
                Console.WriteLine("FAIL: the explicitly chosen cargo token was rewritten");
                failures++;
            }

            // 8) 下拉框目录必须覆盖 DLC 货物（它们在 def.scs 的显示名表里，
            //    但不在费率表里，早期版本因此选不到）。
            bool dlcInPicker = freight.CargoChoices.Any(c => c.Token == "cargo.halloween") &&
                               freight.CargoChoices.Any(c => c.Token == "cargo.halloween_2a");
            Console.WriteLine($"[cargo] DLC cargoes in the picker = {dlcInPicker} " +
                              $"(choices={freight.CargoChoices.Count})");
            if (!dlcInPicker)
            {
                Console.WriteLine("FAIL: the cargo picker is missing the DLC cargoes");
                failures++;
            }

            // 9) 下拉框的清单必须是「游戏实际在用的全部货物」：
            //    - DLC 货物（万圣节、农用机械、工程车…）必须能选到
            //    - 游戏自身从不引用的旧名（cargo_obsole.*）不能选
            //    - **没有 cargo_data 定义、但游戏在用的货物也必须能选到**
            //      （cargo.haulie 是通用货运，游戏自己的任务引用了它 183 次，
            //       只能从存档实测表里拿到 —— 少了它玩家就选不到最常见的货）
            var pickerTokens = new HashSet<string>(
                freight.CargoChoices.Select(c => c.Token), StringComparer.OrdinalIgnoreCase);
            var selectable = new HashSet<string>(
                CargoCatalogue.SelectableTokens, StringComparer.OrdinalIgnoreCase);
            bool pickerMatches = pickerTokens.Count == selectable.Count &&
                                 selectable.All(pickerTokens.Contains);
            bool noObsolete = !pickerTokens.Any(t => t.StartsWith("cargo_obsole.", StringComparison.OrdinalIgnoreCase));
            bool hasGameUsed = pickerTokens.Contains("cargo.haulie") &&
                               pickerTokens.Contains("cargo.haulie2") &&
                               pickerTokens.Contains("cargo.hw25c1") &&
                               pickerTokens.Contains("cargo.hw25c2") &&
                               pickerTokens.Contains("cargo.hw25c3") &&
                               pickerTokens.Contains("cargo.tmp_00");
            Console.WriteLine($"[cargo] picker matches selectable={pickerMatches} " +
                              $"no-obsolete={noObsolete} has-game-used={hasGameUsed} (picker={pickerTokens.Count})");
            if (!pickerMatches || !noObsolete || !hasGameUsed)
            {
                Console.WriteLine("FAIL: the cargo picker does not match the cargo the game actually uses");
                failures++;
            }

            // 10) 清单里的显示名必须两两不重复（撞名的会加 token 后缀）。
            // 与实现的分组口径保持一致（实现用 OrdinalIgnoreCase）：断言不能比实现宽松，
            // 否则将来游戏加入「仅大小写不同」的两个货物名时，断言会漏检而实现会消歧。
            var pickerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int nameDup = 0;
            foreach (var choice in freight.CargoChoices)
            {
                string? previous;
                if (pickerNames.TryGetValue(choice.Name, out previous))
                {
                    nameDup++;
                    Console.WriteLine($"  FAIL: '{choice.Token}' and '{previous}' both show as '{choice.Name}'");
                }
                else
                {
                    pickerNames[choice.Name] = choice.Token;
                }
            }
            Console.WriteLine($"[cargo] duplicate display names in the picker = {nameDup}");
            if (nameDup != 0)
            {
                Console.WriteLine("FAIL: " + nameDup + " cargo name(s) in the picker are not distinguishable");
                failures++;
            }

            // 10) 老 token（不在下拉框里，但存档里还在用）也必须原样写进存档。
            freight.NewCargo = "cargo.haulie";
            int jobsBeforeLegacy = sf.GetFreightJobs().Count;
            freight.CreateJobCommand.Execute(null);

            var jobsLegacy = sf.GetFreightJobs();
            var createdLegacy = jobsLegacy.Count > jobsBeforeLegacy
                ? jobsLegacy[jobsLegacy.Count - 1]
                : null;
            bool legacyKept = createdLegacy != null && createdLegacy.Cargo == "cargo.haulie";
            Console.WriteLine($"[cargo] legacy cargo token kept = {legacyKept} " +
                              $"(cargo={createdLegacy?.Cargo ?? "(none)"})");
            if (!legacyKept)
            {
                Console.WriteLine("FAIL: a legacy cargo token was rewritten");
                failures++;
            }

            return failures;
        }

        // ------------------------------------------------------------------
        // 货物显示名：存档里出现过的每一个货物 token 都必须显示得可读。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验「存档里正在用的所有货物都显示得可读」。
        /// <para>
        /// 存档是历史累积的：玩家早期版本接过的任务，<c>cargo</c> 字段仍写着
        /// 当年的 token（<c>cargo.haulie</c>、<c>cargo.cable_reel</c> …），而这些
        /// token 在 <c>def.scs</c> 的 <c>cargo_data</c> 里已经没有了。显示名必须
        /// 有三级兜底：权威表 → 本地化表（locale.scs 的 cn_* 键）→ token 可读形式。
        /// </para>
        /// </summary>
        private static int TestCargoNames()
        {
            int failures = 0;

            if (!File.Exists(RealSavePath))
            {
                Console.WriteLine("[cargonames] SKIP: real save not found");
                return 0;
            }

            string dir = CreateTempDir("cargonames");
            string copy = Path.Combine(dir, "cargonames_copy.sii");
            File.Copy(RealSavePath, copy, true);   // 只操作副本

            var sf = new SaveFile();
            sf.Load(copy);

            // 收集存档里出现过的货物 token。
            var tokens = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var unit in sf.Document.GetUnitsByType("job_offer_data"))
            {
                string cargo = unit.GetReference("cargo");
                if (!string.IsNullOrEmpty(cargo) && cargo.StartsWith("cargo.", StringComparison.Ordinal))
                {
                    tokens.Add(cargo);
                }
            }
            foreach (var unit in sf.Document.GetUnitsByType("delivery_log_entry"))
            {
                var items = unit.GetArrayItems("params");
                if (items.Count <= 3) continue;
                string cargo = items[3].AsId();
                if (!string.IsNullOrEmpty(cargo) && cargo.StartsWith("cargo.", StringComparison.Ordinal))
                {
                    tokens.Add(cargo);
                }
            }

            // 游戏自己生成的任务引用过的每一个货物 token，都必须能在可选清单里
            // 找到 —— 否则用户想建一个和游戏里一样的任务却选不到那种货。
            // （cargo.haulie 这类「没有 cargo_data 定义、但游戏在用」的货物
            //   最容易漏：只用定义表做清单就会漏掉它们。）
            var selectableSet = new HashSet<string>(
                CargoCatalogue.SelectableTokens, StringComparer.OrdinalIgnoreCase);
            int notSelectable = 0;
            foreach (string token in tokens)
            {
                if (selectableSet.Contains(token)) continue;
                notSelectable++;
                Console.WriteLine($"  FAIL: '{token}' is used by the game but is not selectable");
            }
            Console.WriteLine($"[cargonames] game-used tokens not selectable = {notSelectable}");
            if (notSelectable != 0)
            {
                Console.WriteLine("FAIL: " + notSelectable + " cargo token(s) the game uses are not selectable");
                failures++;
            }

            int blank = 0;
            int withResidue = 0;
            int viaNames = 0;
            int viaLocale = 0;
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string token in tokens)
            {
                string name = CargoCatalogue.DisplayName(token);
                if (string.IsNullOrWhiteSpace(name)) { blank++; continue; }

                // 撞名的货物会带 " (token)" 后缀，这是有意为之；去掉后不应再有
                // 原始 token 残留或本地化占位符。
                string bare = name;
                int bracket = name.LastIndexOf(" (", StringComparison.Ordinal);
                if (bracket > 0 && name.EndsWith(")", StringComparison.Ordinal))
                {
                    bare = name.Substring(0, bracket);
                }
                if (bare.IndexOf("cargo.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    bare.IndexOf("@@", StringComparison.Ordinal) >= 0)
                {
                    withResidue++;
                    Console.WriteLine($"  FAIL: '{token}' -> '{name}'");
                }

                // 不同 token 不能显示成同一个名字。
                string? previous;
                if (names.TryGetValue(name, out previous))
                {
                    withResidue++;
                    Console.WriteLine($"  FAIL: '{token}' and '{previous}' both show as '{name}'");
                }
                else
                {
                    names[name] = token;
                }

                if (GameNames.Lookup(GameNames.Cargo, token).Length > 0) viaNames++;
                else if (GameNames.Lookup(GameNames.LocalizedCargo, token).Length > 0) viaLocale++;
            }

            Console.WriteLine($"[cargonames] tokens in save={tokens.Count} via-names-table={viaNames} " +
                              $"via-locale={viaLocale} blank={blank} residue/collision={withResidue}");
            if (blank != 0)
            {
                Console.WriteLine("FAIL: " + blank + " cargo token(s) have no readable name");
                failures++;
            }
            if (withResidue != 0)
            {
                Console.WriteLine("FAIL: " + withResidue + " cargo name(s) are unreadable or not distinguishable");
                failures++;
            }

            // 本地化兜底表必须真的能兜住一些 cargo_data 没收的 token。
            bool localeUseful = GameNames.Lookup(GameNames.LocalizedCargo, "cargo.driller") == "Driller D-50" &&
                                GameNames.Lookup(GameNames.LocalizedCargo, "cargo.cable_reel") == "Industrial Cable Reel" &&
                                GameNames.Lookup(GameNames.LocalizedCargo, "cargo.planter") == "Planter";
            Console.WriteLine($"[cargonames] localized fallback usable = {localeUseful} " +
                              $"(table={GameNames.LocalizedCargo.Count} entries)");
            if (!localeUseful)
            {
                Console.WriteLine("FAIL: the localized cargo fallback table does not cover the known tokens");
                failures++;
            }

            // 查不到任何名字的老 token 也要显示成可读形式（首字母大写）。
            bool legacyOk = CargoCatalogue.DisplayName("cargo.haulie") == "Haulie" &&
                            CargoCatalogue.DisplayName("cargo.hw25c1") == "Hw25c1" &&
                            CargoCatalogue.DisplayName("cargo.tmp_00") == "Tmp 00";
            Console.WriteLine($"[cargonames] legacy token fallback = '{CargoCatalogue.DisplayName("cargo.haulie")}' / " +
                              $"'{CargoCatalogue.DisplayName("cargo.hw25c1")}' / " +
                              $"'{CargoCatalogue.DisplayName("cargo.tmp_00")}'");
            if (!legacyOk)
            {
                Console.WriteLine("FAIL: a legacy cargo token is not shown in a readable form");
                failures++;
            }

            return failures;
        }

        /// <summary>
        /// 校验「空引用」的统一处理。
        /// <para>
        /// 存档里的空引用存的是**字面量 <c>null</c>**（不是空串），数组头
        /// 又是整数。两者都必须当成「没有引用」，否则会把 351 个待聘司机
        /// 模板之类的占位符当成真实数据。
        /// </para>
        /// </summary>
        private static int TestNullReference()
        {
            int failures = 0;

            // 1) 扩展方法本身。
            bool helperOk =
                SiiValueExtensions.AsReferenceId(null).Length == 0 &&
                SiiValueExtensions.AsReferenceId(new ReferenceValue("null")).Length == 0 &&
                SiiValueExtensions.AsReferenceId(new IntValue(3)).Length == 0 &&
                SiiValueExtensions.AsReferenceId(new ReferenceValue("unit.1")) == "unit.1" &&
                SiiValueExtensions.NormalizeReferenceId(null).Length == 0 &&
                SiiValueExtensions.NormalizeReferenceId("null").Length == 0 &&
                SiiValueExtensions.NormalizeReferenceId("garage.calais") == "garage.calais";
            Console.WriteLine($"[nullref] helper behaviour ok = {helperOk}");
            if (!helperOk)
            {
                Console.WriteLine("FAIL: the reference-id helper does not filter empty references");
                failures++;
            }

            // 2) 带空位占位符的文档：占位符不得进入任何列表。
            string sample =
                "SiiNunit\n{\n" +
                "economy : _nameless.0000 {\n experience_points: 1000\n}\n" +
                "player : _nameless.0001 {\n" +
                " trucks: 2\n trucks[0]: unit.0\n trucks[1]: null\n" +
                " drivers: 2\n drivers[0]: null\n drivers[1]: null\n}\n" +
                "garage : garage.calais {\n" +
                " vehicles: 2\n vehicles[0]: unit.0\n vehicles[1]: null\n" +
                " drivers: 1\n drivers[0]: null\n}\n" +
                "vehicle : unit.0 {\n}\n" +
                "}\n";

            SiiDocument doc = SiiParser.Parse(sample);

            var garage = new Garage(doc.GetUnitById("garage.calais"));
            var player = Player.FromDocument(doc);

            bool garageOk = garage.Vehicles.Count == 1 && garage.Vehicles[0] == "unit.0" &&
                            garage.Drivers.Count == 0 && garage.TrailerCount == 0;
            bool playerOk = player != null &&
                            player.OwnedTrucks.Count == 1 && player.OwnedTrucks[0] == "unit.0" &&
                            player.Drivers.Count == 0;
            bool indexOk = doc.FindGarageOwner("null").Length == 0 &&
                           doc.FindGarageOwner("unit.0") == "garage.calais";
            Console.WriteLine($"[nullref] garage={garageOk} player={playerOk} index={indexOk}");
            if (!garageOk || !playerOk || !indexOk)
            {
                Console.WriteLine("FAIL: the 'null' placeholder leaked into a list");
                failures++;
            }

            return failures;
        }

        // ------------------------------------------------------------------
        // 文档里的数量必须与代码里的实际数据一致。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验 README / ARCHITECTURE 里写着的数量与代码实际值一致。
        /// <para>
        /// 背景：这一轮真实出现过两处陈旧数字 —— README 曾写着「355 个城市」
        /// （实际 334），以及「漏掉 33 种货物」（DLC 合并把 cargo_data 从 397
        /// 扩到 489 之后，那个数字就悄悄失效了，合并后只剩个位数）。
        /// 它们的共同点是：**数字来自某次实测，在底层数据变化后就悄悄失效了**，
        /// 没人重新核对。
        /// </para>
        /// <para>
        /// 这里把文档里的关键数量与代码实际值绑定：期望值**不硬编码**，一律取自
        /// 目录本身，所以以后再合并 DLC、再改表规模，文档与断言会一起对。
        /// </para>
        /// </summary>
        private static int TestDocNumbers()
        {
            int failures = 0;

            string? readme = FindRepoFile("README.md");
            if (readme == null)
            {
                Console.WriteLine("[docnum] SKIP: 不在仓库内运行（找不到 README.md）");
                return 0;
            }
            string readmeText = File.ReadAllText(readme);

            string? arch = FindRepoFile(Path.Combine("docs", "ARCHITECTURE.md"));
            string archText = arch == null ? string.Empty : File.ReadAllText(arch);

            int selectableCount = CargoCatalogue.SelectableTokens.Count();

            var claims = new (string Label, string Text, string Pattern, int Expected)[]
            {
                ("README 城市数", readmeText, @"城市（(\d+) 个", CityCatalogue.Count),
                ("README 货物页目录", readmeText, @"与货物（(\d+) 种", selectableCount),
                ("README 货物名表", readmeText, @"cargo_data`（(\d+) 项）", GameNames.Cargo.Count),
                ("README 可选清单", readmeText, @"SelectableTokens`（(\d+) 项）", selectableCount),
                ("README 本地化兜底", readmeText, @"cn_\*` 键，(\d+) 项）", GameNames.LocalizedCargo.Count),
                ("ARCH 城市数", archText, @"（(\d+) 个中文城市）", CityCatalogue.Count),
                ("ARCH 货物数", archText, @"SelectableTokens`（(\d+) 个", selectableCount),
                ("ARCH 公司数", archText, @"（(\d+) 家货运公司）", GameNames.Company.Count),
            };

            foreach (var claim in claims)
            {
                var match = Regex.Match(claim.Text, claim.Pattern);
                if (!match.Success)
                {
                    Console.WriteLine($"  FAIL: {claim.Label} —— 文档里找不到匹配「{claim.Pattern}」的数量表述");
                    failures++;
                    continue;
                }

                int stated = int.Parse(match.Groups[1].Value);
                bool ok = stated == claim.Expected;
                Console.WriteLine($"[docnum] {claim.Label}: 文档={stated} 实际={claim.Expected} {(ok ? "✓" : "✗")}");
                if (!ok)
                {
                    Console.WriteLine("FAIL: " + claim.Label + " 与代码实际值不一致");
                    failures++;
                }
            }

            return failures;
        }

        /// <summary>
        /// 从测试程序所在目录向上查找仓库里的某个文件；找不到返回 null。
        /// </summary>
        private static string? FindRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // WPF：菜单里的保存项必须与工具栏一致。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验菜单里的「保存 / 另存为…」与工具栏按钮的可用状态一致。
        /// <para>
        /// 这一项此前**只有 QA 的运行时探针覆盖过，仓库测试里一次都没有** ——
        /// 也就是说谁把这两个菜单项的 <c>IsEnabled="{Binding HasLoadedSave}"</c>
        /// 弄掉了，仓库测试仍然是全绿的。
        /// </para>
        /// <para>
        /// WPF 的三个坑（QA 实测踩出来的）：
        /// ① <c>Application</c> 全进程只能有一个实例；
        /// ② 窗口必须在 **STA 线程**上创建；
        /// ③ 必须先调 <c>App.InitializeComponent()</c>，否则 MainWindow.xaml 里的
        /// <c>StaticResource</c> 找不到，构造直接抛异常。
        /// </para>
        /// </summary>
        private static int TestUiMenuEnable()
        {
            int failures = 0;

            if (!File.Exists(RealSavePath))
            {
                Console.WriteLine("[uimenu] SKIP: real save not found");
                return 0;
            }

            string dir = CreateTempDir("uimenu");
            string copy = Path.Combine(dir, "uimenu_copy.sii");
            File.Copy(RealSavePath, copy, true);   // 只操作副本

            int inner = 0;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    inner = RunUiMenuCheck(copy);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FAIL: WPF 窗口测试抛异常：" + ex.GetType().Name + ": " + ex.Message);
                    inner = 1;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            return failures + inner;
        }

        /// <summary>在 STA 线程上执行的 WPF 断言主体。</summary>
        private static int RunUiMenuCheck(string savePath)
        {
            int failures = 0;

            // 坑 ①：Application 全进程只能有一个实例；坑 ③：必须先 InitializeComponent()。
            if (System.Windows.Application.Current == null)
            {
                var app = new App();
                app.InitializeComponent();
            }

            var window = new MainWindow();

            // MainWindow 的构造函数是「先 InitializeComponent()、再赋 DataContext」，
            // WPF 会把「DataContext 变了」的绑定刷新排到 Dispatcher 队列里，
            // 所以这里要 Pump 一下才能读到绑定生效后的 IsEnabled。
            PumpDispatcher();

            // x:Name 生成的字段是 internal，用反射取。
            var miSave = GetUiElement(window, "MiSave");
            var miSaveAs = GetUiElement(window, "MiSaveAs");
            var btnSave = GetUiElement(window, "BtnSave");
            var btnSaveAs = GetUiElement(window, "BtnSaveAs");
            if (miSave == null || miSaveAs == null || btnSave == null || btnSaveAs == null)
            {
                Console.WriteLine("FAIL: 找不到菜单/工具栏控件（MiSave / MiSaveAs / BtnSave / BtnSaveAs）");
                return 1;
            }

            // 未加载存档 → 四个控件都应禁用。
            bool disabled = !miSave.IsEnabled && !miSaveAs.IsEnabled &&
                            !btnSave.IsEnabled && !btnSaveAs.IsEnabled;
            Console.WriteLine($"[uimenu] 未加载存档时四个控件都禁用 = {disabled}");
            if (!disabled)
            {
                Console.WriteLine("FAIL: 未加载存档时菜单/工具栏的保存项没有禁用");
                failures++;
            }

            // 载入存档 → 四个控件都应可用，且菜单与工具栏状态一致。
            window.VM.LoadFile(savePath);
            PumpDispatcher();

            bool enabled = miSave.IsEnabled && miSaveAs.IsEnabled &&
                           btnSave.IsEnabled && btnSaveAs.IsEnabled;
            bool consistent = miSave.IsEnabled == btnSave.IsEnabled &&
                              miSaveAs.IsEnabled == btnSaveAs.IsEnabled;
            Console.WriteLine($"[uimenu] 加载后四个控件都可用 = {enabled}，菜单与工具栏一致 = {consistent}");
            if (!enabled || !consistent)
            {
                Console.WriteLine("FAIL: 加载存档后菜单/工具栏的保存项没有同步启用");
                failures++;
            }

            return failures;
        }

        /// <summary>取窗口里 x:Name 命名的控件（字段是 internal，只能反射）。</summary>
        private static System.Windows.UIElement? GetUiElement(object window, string name)
        {
            var field = window.GetType().GetField(
                name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            return field?.GetValue(window) as System.Windows.UIElement;
        }

        /// <summary>把 Dispatcher 队列里排队的更新跑完（绑定刷新可能排队）。</summary>
        private static void PumpDispatcher()
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        // ------------------------------------------------------------------
        // 源码里不得出现指向真实存档目录的硬编码路径。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验仓库源码里没有指向**真实存档目录**的硬编码路径。
        /// <para>
        /// 背景：本工具保存时写出的是**明文** SII，而原始加密存档不可再生（HMAC 密钥
        /// 未公开）—— 一旦有程序往真实存档上写了哪怕一把，用户的加密原档就被明文顶替了。
        /// 而**即使程序本身不调用保存，只要它加载了真实存档并渲染界面，就存在触发写入的
        /// 路径**：不能依赖「我知道它不会写」。真实存档目录里那两份 5.3 MB 的明文
        /// <c>game_backup.sii</c>（游戏自己保存时的旧档备份）说明这套存档历史上被以明文
        /// 形式覆盖过，自动化程序误用真实存档是**真实存在的口子**（离屏渲染 / 截图工具
        /// 最容易踩）。所以这条约束要由机器保证，而不是靠自觉。
        /// </para>
        /// <para>
        /// 两条检查：
        /// ① 源码（src/ tests/ tools/，<b>排除注释</b>）里不得出现「盘符绝对路径 +
        /// 真实存档目录特征」的组合；唯一豁免是测试项目里那处**只读基准**常量
        /// <c>RealSavePath</c> 的声明行（它只被复制，从不写入）。
        /// ② 测试项目里 <c>RealSavePath</c> 的**用法**必须只读（存在性检查 / 复制 /
        /// 读取），任何写操作都会被抓出来。
        /// </para>
        /// </summary>
        private static int TestNoRealSavePath()
        {
            int failures = 0;

            string? root = FindRepoRoot();
            if (root == null)
            {
                Console.WriteLine("[nosavepath] SKIP: 不在仓库内运行（找不到 .sln）");
                return 0;
            }

            // 真实存档目录的特征：盘符绝对路径 + 「Euro Truck Simulator」或「profiles」。
            var drivePath = new System.Text.RegularExpressions.Regex(@"[A-Za-z]:[\\/]");
            var markers = new[] { "Euro Truck Simulator", "profiles" };
            // 只读基准常量的标识符（用词边界，避免匹配到 TestNoRealSavePath 这类名字）。
            var baselineId = new System.Text.RegularExpressions.Regex(
                @"(?<![A-Za-z0-9_])RealSavePath(?![A-Za-z0-9_])");
            // 任何写操作 API：出现在基准路径附近就是危险用法。
            var writeApis = new[]
            {
                "File.WriteAllText", "File.WriteAllBytes", "File.WriteAllLines",
                "File.Delete", "File.Move", "File.Create", "File.Replace",
                "new FileStream", "new StreamWriter",
            };

            foreach (string dir in new[] { "src", "tests", "tools" })
            {
                string full = Path.Combine(root, dir);
                if (!Directory.Exists(full)) continue;

                foreach (string file in Directory.GetFiles(full, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".cs" && ext != ".py") continue;
                    if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                        file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;

                    bool isPython = ext == ".py";
                    bool inBlockComment = false;
                    int lineNo = 0;
                    foreach (string raw in File.ReadAllLines(file))
                    {
                        lineNo++;
                        string code = StripComments(raw, isPython, ref inBlockComment);
                        if (code.Length == 0) continue;

                        // ① 硬编码真实存档路径（只读基准常量的声明行除外）。
                        if (drivePath.IsMatch(code) &&
                            markers.Any(m => code.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            bool isBaselineDeclaration =
                                baselineId.IsMatch(code) && code.Contains("=");
                            if (!isBaselineDeclaration)
                            {
                                Console.WriteLine($"  FAIL: {Path.GetRelativePath(root, file)}:{lineNo} 硬编码了真实存档路径");
                                Console.WriteLine($"        {raw.Trim()}");
                                failures++;
                            }
                        }

                        // ② 只读基准只能被只读使用。
                        if (baselineId.IsMatch(code) &&
                            writeApis.Any(w => code.Contains(w, StringComparison.Ordinal)))
                        {
                            Console.WriteLine($"  FAIL: {Path.GetRelativePath(root, file)}:{lineNo} 对真实存档做了写操作");
                            Console.WriteLine($"        {raw.Trim()}");
                            failures++;
                        }
                    }
                }
            }

            Console.WriteLine($"[nosavepath] 源码里没有指向真实存档目录的硬编码路径 = {failures == 0}");
            return failures;
        }

        /// <summary>
        /// 去掉一行里的注释（字符串字面量里的 // 与 # 不算注释）。
        /// </summary>
        private static string StripComments(string line, bool isPython, ref bool inBlockComment)
        {
            var builder = new System.Text.StringBuilder();
            bool inString = false;
            bool inChar = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inBlockComment)
                {
                    if (c == '*' && i + 1 < line.Length && line[i + 1] == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (inString)
                {
                    builder.Append(c);
                    if (c == '\\' && i + 1 < line.Length) { builder.Append(line[++i]); continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    builder.Append(c);
                    if (c == '\\' && i + 1 < line.Length) { builder.Append(line[++i]); continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }

                if (c == '"') { inString = true; builder.Append(c); continue; }
                if (!isPython && c == '\'') { inChar = true; builder.Append(c); continue; }

                if (isPython && c == '#')
                {
                    break;   // 行注释
                }
                if (!isPython && c == '/' && i + 1 < line.Length)
                {
                    if (line[i + 1] == '/') break;                    // 行注释
                    if (line[i + 1] == '*') { inBlockComment = true; i++; continue; }
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>找到仓库根目录（含 .sln 的那一层）；找不到返回 null。</summary>
        private static string? FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.GetFiles(dir.FullName, "*.sln").Length > 0) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>
        /// 校验货物显示名的查表、跨源消歧与回退。
        /// <para>
        /// ① **跨组同名**：同一个显示名分别出现在权威表（<c>GameNames.Cargo</c>）
        /// 与本地化兜底表（<c>GameNames.LocalizedCargo</c>）时，两边都要查得到，
        /// 并且必须被消歧成两个不同的名字。
        /// ② **尾段区分**：像 <c>cargo.concr_beams</c> / <c>cargo.concr_beams2</c>
        /// 这种尾段不同的两个货物，加后缀后不再相同。
        /// ③ **大小写不敏感**：查表用 <c>OrdinalIgnoreCase</c>，大小写变体也要命中。
        /// ④ **目录外 token**：不在任何表里的 token 回退成可读形式，且不带
        /// <c>cargo.</c> 前缀残留、不含 <c>@@</c> 占位符。
        /// ⑤ **可选清单两两不重复**：清单里每个 token 的显示名都不重复。
        /// </para>
        /// </summary>
        private static int TestDisplayName()
        {
            int failures = 0;

            // ① 跨组同名：cargo.concr_beams 只在本地化表里（cn_concr_beams），
            //    cargo.concr_beams2 在权威表里 —— 两者基础名都是 "Concrete Beams"。
            string fromLocalized = GameNames.Lookup(GameNames.LocalizedCargo, "cargo.concr_beams");
            string fromNames = GameNames.Lookup(GameNames.Cargo, "cargo.concr_beams2");
            bool sameBase = fromLocalized.Length > 0 && fromLocalized == fromNames;
            string nameA = CargoCatalogue.DisplayName("cargo.concr_beams");
            string nameB = CargoCatalogue.DisplayName("cargo.concr_beams2");
            bool crossOk = sameBase && nameA != nameB && nameA.Length > 0 && nameB.Length > 0;
            Console.WriteLine($"[displayname] 跨源同名消歧 = {crossOk}  ('{nameA}' vs '{nameB}')");
            if (!crossOk)
            {
                Console.WriteLine("FAIL: 同一显示名跨两个来源时没有被正确消歧");
                failures++;
            }

            // ② 尾段区分：加后缀后两个名字必须不同，且都保留了基础名。
            bool tailOk = nameA.StartsWith(fromLocalized, StringComparison.Ordinal) &&
                          nameB.StartsWith(fromNames, StringComparison.Ordinal) &&
                          nameA.Contains("cargo.concr_beams") &&
                          nameB.Contains("cargo.concr_beams2") &&
                          nameA != nameB;
            Console.WriteLine($"[displayname] 尾段区分 = {tailOk}");
            if (!tailOk)
            {
                Console.WriteLine("FAIL: 尾段不同的两个货物没有分别加后缀");
                failures++;
            }

            // ③ 大小写不敏感：大小写变体要命中同一张表、得到同一个名字。
            string upper = CargoCatalogue.DisplayName("CARGO.GUMMY_BEARS");
            bool caseOk = upper == "Gummy Bears";
            // 目录外 token 的回退形式沿用 token 自身的大小写（HW25C1 这类代号不该被
            // 改成 Hw25c1），所以这里只要求「非空 + 不带前缀残留」。
            string upperFallback = CargoCatalogue.DisplayName("CARGO.NOT_IN_ANY_TABLE");
            bool caseFallbackOk = !string.IsNullOrWhiteSpace(upperFallback) &&
                                 upperFallback.IndexOf("cargo.", StringComparison.OrdinalIgnoreCase) < 0;
            Console.WriteLine($"[displayname] 大小写不敏感 = {caseOk && caseFallbackOk}  ('{upper}')");
            if (!caseOk || !caseFallbackOk)
            {
                Console.WriteLine("FAIL: 显示名查表对大小写变体不一致");
                failures++;
            }

            // ④ 目录外 token：回退成可读形式，不带前缀残留、不含占位符。
            string outside = CargoCatalogue.DisplayName("cargo.some_unknown_cargo");
            bool outsideOk = !string.IsNullOrWhiteSpace(outside) &&
                             outside.IndexOf("cargo.", StringComparison.OrdinalIgnoreCase) < 0 &&
                             outside.IndexOf("@@", StringComparison.Ordinal) < 0;
            Console.WriteLine($"[displayname] 目录外 token 回退 = '{outside}'  ({outsideOk})");
            if (!outsideOk)
            {
                Console.WriteLine("FAIL: 目录外 token 的回退显示名不可读");
                failures++;
            }

            // ⑤ 可选清单两两不重复。
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int dup = 0;
            int blank = 0;
            foreach (string token in CargoCatalogue.SelectableTokens)
            {
                string name = CargoCatalogue.DisplayName(token);
                if (string.IsNullOrWhiteSpace(name)) { blank++; continue; }
                string? previous;
                if (names.TryGetValue(name, out previous))
                {
                    dup++;
                    Console.WriteLine($"  FAIL: '{token}' and '{previous}' both show as '{name}'");
                }
                else
                {
                    names[name] = token;
                }
            }
            Console.WriteLine($"[displayname] 可选清单显示名重复 = {dup}，空白 = {blank}");
            if (dup != 0 || blank != 0)
            {
                Console.WriteLine("FAIL: 可选清单的显示名有重复或空白");
                failures++;
            }

            return failures;
        }

        /// <summary>文档里出现过的全部单元类型（去重、按字典序）。</summary>
        private static List<string> AllTypesOf(SiiDocument doc)
        {
            var types = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unit in doc.Units)
            {
                if (unit == null) continue;
                if (seen.Add(unit.Type)) types.Add(unit.Type);
            }
            types.Sort(StringComparer.Ordinal);
            return types;
        }

        /// <summary>
        /// 货运市场测试用的最小存档：两家公司、两条任务。
        /// </summary>
        private const string FreightSample =
            "SiiNunit\n{\n" +
            "economy : _nameless.0000 {\n experience_points: 1000\n}\n" +
            "company : company.volatile.nbfc.aberdeen {\n" +
            " permanent_data: company.permanent.nbfc\n" +
            " job_offer: 1\n job_offer[0]: _nameless.job1\n discovered: true\n}\n" +
            "company : company.volatile.stokes.erfurt {\n" +
            " permanent_data: company.permanent.stokes\n" +
            " job_offer: 1\n job_offer[0]: _nameless.job2\n discovered: true\n}\n" +
            "job_offer_data : _nameless.job1 {\n" +
            " target: \"nbfc.aberdeen\"\n cargo: cargo.haulie\n company_truck: scania_s_2024e\n" +
            " urgency: 0\n units_count: 1\n fill_ratio: 1\n shortest_distance_km: 100\n}\n" +
            "job_offer_data : _nameless.job2 {\n" +
            " target: \"stokes.erfurt\"\n cargo: cargo.gummy_bears\n company_truck: daf_xg\n" +
            " urgency: 0\n units_count: 1\n fill_ratio: 1\n shortest_distance_km: 200\n}\n" +
            "}\n";

        // ------------------------------------------------------------------
        // 挂车型号：缺少 trailer_def 单元时的降级显示。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验挂车「型号」的降级链：
        /// 有 trailer_def 单元 → 用它的 source_name；没有（DLC 挂车必然如此）
        /// → 用挂车定义本身拼一个可读名称；两样都没有 → 至少显示定义原值，
        /// 绝不能留下空白。
        /// </summary>
        private static int TestTrailerModelName()
        {
            int failures = 0;

            string sample =
                "SiiNunit\n{\n" +
                "trailer_def : _nameless.def1 {\n" +
                " source_name: trailer_def.scs.gooseneck.singl_3e_40.container\n}\n" +
                "trailer : _nameless.t1 {\n trailer_definition: _nameless.def1\n}\n" +
                "trailer : _nameless.t2 {\n trailer_definition: trailer_def.halloween_1.scs_dryhal26\n}\n" +
                "trailer : _nameless.t3 {\n trailer_definition: _nameless.missing\n}\n" +
                "trailer : _nameless.t4 {\n trailer_definition: null\n}\n" +
                "trailer : _nameless.t5 {\n}\n" +
                "}\n";

            SiiDocument doc = SiiParser.Parse(sample);

            // 1) 有 trailer_def 单元：走 source_name。
            var withDef = new Trailer(doc.GetUnitById("_nameless.t1"), doc);
            bool withDefOk = withDef.ModelName == "Gooseneck Container";
            Console.WriteLine($"[trailer] with trailer_def unit -> '{withDef.ModelName}' (expect Gooseneck Container)");
            if (!withDefOk)
            {
                Console.WriteLine("FAIL: trailer model name from source_name is wrong: " + withDef.ModelName);
                failures++;
            }

            // 2) DLC 挂车：存档里没有 trailer_def 单元，必须降级成可读名称。
            //    断言「非空」+「含可读片段」+「不是原始 token」，任何一种降级
            //    链被改断都会立刻失败。
            var dlc = new Trailer(doc.GetUnitById("_nameless.t2"), doc);
            bool dlcOk = dlc.ModelName == "Halloween 1 Scs Dryhal26";
            bool dlcReadable = !string.IsNullOrWhiteSpace(dlc.ModelName) &&
                               dlc.ModelName.IndexOf("Halloween", StringComparison.OrdinalIgnoreCase) >= 0 &&
                               dlc.ModelName.IndexOf('_') < 0;
            Console.WriteLine($"[trailer] DLC trailer -> '{dlc.ModelName}' (expect Halloween 1 Scs Dryhal26)");
            if (!dlcOk || !dlcReadable)
            {
                Console.WriteLine("FAIL: DLC trailer model name fallback is wrong: " + dlc.ModelName);
                failures++;
            }

            // 2b) 用一个「引用了不存在的 trailer_def 单元」的挂车单元单独验证：
            //     这是 DLC 挂车在存档里的真实形态，必须得到可读名称而不是空串。
            var dlcUnit = new SiiUnit("trailer", "_nameless.dlc");
            dlcUnit.SetScalar("trailer_definition", "trailer_def.halloween_1.scs_dryhal26");
            var dlcOnly = new Trailer(dlcUnit, doc);
            bool dlcOnlyOk = !string.IsNullOrWhiteSpace(dlcOnly.ModelName) &&
                             dlcOnly.ModelName.IndexOf("Halloween", StringComparison.OrdinalIgnoreCase) >= 0 &&
                             dlcOnly.SourceName.Length == 0;   // SourceName 语义不变：空就是空
            Console.WriteLine($"[trailer] missing trailer_def unit -> '{dlcOnly.ModelName}' " +
                              $"(source='{dlcOnly.SourceName}')");
            if (!dlcOnlyOk)
            {
                Console.WriteLine("FAIL: a trailer referencing a missing trailer_def unit has no readable model name");
                failures++;
            }

            // 3) 悬空引用：至少显示定义原值，不能是空串。
            var dangling = new Trailer(doc.GetUnitById("_nameless.t3"), doc);
            bool danglingOk = !string.IsNullOrEmpty(dangling.ModelName) &&
                              dangling.ModelName == dangling.Definition;
            Console.WriteLine($"[trailer] dangling reference -> '{dangling.ModelName}' (expect the raw definition)");
            if (!danglingOk)
            {
                Console.WriteLine("FAIL: a dangling trailer reference produced an empty model name");
                failures++;
            }

            // 4) 连定义都没有：至少给出单元 id，绝不留空白。
            var noDefinition = new Trailer(doc.GetUnitById("_nameless.t4"), doc);
            var noAttribute = new Trailer(doc.GetUnitById("_nameless.t5"), doc);
            bool noDefinitionOk = noDefinition.Definition == string.Empty &&
                                  noDefinition.ModelName == "_nameless.t4" &&
                                  noAttribute.ModelName == "_nameless.t5";
            Console.WriteLine($"[trailer] without definition -> '{noDefinition.ModelName}' / " +
                              $"'{noAttribute.ModelName}' (expect the unit ids)");
            if (!noDefinitionOk)
            {
                Console.WriteLine("FAIL: a trailer without a definition produced an empty model name");
                failures++;
            }

            // 5) 真实存档：每一辆挂车的型号都必须有内容。
            if (!File.Exists(RealSavePath))
            {
                Console.WriteLine("[trailer] SKIP: real save not found");
                return failures;
            }

            string dir = CreateTempDir("trailer");
            string copy = Path.Combine(dir, "trailer_copy.sii");
            File.Copy(RealSavePath, copy, true);   // 只操作副本

            var sf = new SaveFile();
            sf.Load(copy);

            int blank = 0;
            int fallback = 0;
            int rawToken = 0;
            foreach (var trailer in sf.GetTrailers())
            {
                if (string.IsNullOrEmpty(trailer.ModelName)) blank++;
                if (string.IsNullOrEmpty(trailer.SourceName)) fallback++;

                // 型号必须是整理过的可读名称，不能直接把原始 token 甩给用户。
                if (trailer.ModelName.IndexOf('_') >= 0 || trailer.ModelName.IndexOf('.') >= 0) rawToken++;
            }

            Console.WriteLine($"[trailer] real save: trailers={sf.GetTrailers().Count} blank={blank} " +
                              $"without-trailer_def={fallback} raw-token={rawToken}");
            if (rawToken != 0)
            {
                Console.WriteLine("FAIL: " + rawToken + " trailer(s) still show a raw token instead of a readable name");
                failures++;
            }
            if (blank != 0)
            {
                Console.WriteLine("FAIL: " + blank + " trailer(s) still show an empty model name");
                failures++;
            }

            // 存档里没有 trailer_def 单元的挂车（DLC 挂车）必须走降级路径。
            if (fallback == 0)
            {
                Console.WriteLine("FAIL: the real save should contain a DLC trailer without trailer_def unit");
                failures++;
            }

            return failures;
        }

        // ------------------------------------------------------------------
        // 内置游戏数据：城市与货物必须来自权威数据源。
        // ------------------------------------------------------------------

        /// <summary>
        /// 校验内置游戏数据：
        /// 城市来自 <see cref="CityCatalogue"/>（中文名、覆盖全部城市），
        /// 货物来自 <see cref="CargoCatalogue.Tokens"/>（显示名表 + 费率表 +
        /// 实测费率表合并出的真实游戏 token），返回的都是副本。
        /// </summary>
        private static int TestBuiltInGameData()
        {
            int failures = 0;
            var data = new BuiltInGameData();

            // 1) 城市：数量与目录一致，名称是中文。
            var cities = data.Cities();
            bool cityCountOk = cities.Count == CityCatalogue.Count && cities.Count > 300;
            var london = cities.FirstOrDefault(c => c.Token == "london");
            bool cityNameOk = london != null && london.Name == "伦敦";
            Console.WriteLine($"[gamedata] cities={cities.Count} (catalogue {CityCatalogue.Count}) " +
                              $"london='{london?.Name ?? "(null)"}'");
            if (!cityCountOk)
            {
                Console.WriteLine("FAIL: the built-in city list does not match the city catalogue");
                failures++;
            }
            if (!cityNameOk)
            {
                Console.WriteLine("FAIL: the built-in city list is not using the Chinese names");
                failures++;
            }

            // 2) 返回的必须是副本：改它不能污染全局目录。
            //    City 本身是不可变的，所以共享元素也不会被改坏。
            cities.Clear();
            bool copyOk = CityCatalogue.Cities().Count == CityCatalogue.Count &&
                          data.Cities().Count == CityCatalogue.Count;
            if (!copyOk)
            {
                Console.WriteLine("FAIL: the built-in city list exposes the catalogue's internal list");
                failures++;
            }

            // 5) 货物目录：必须是真实 token，旧的虚构 token（oil / milk …）必须消失。
            var cargoes = data.Cargoes();
            var selectableCount = CargoCatalogue.SelectableTokens.Count();
            var gummy = cargoes.FirstOrDefault(c => c.Token == "cargo.gummy_bears");
            bool cargoCountOk = cargoes.Count == selectableCount && cargoes.Count > 300;
            bool cargoTokenOk = gummy != null && gummy.Name == "Gummy Bears";
            bool noFakeOk = !cargoes.Any(c => c.Token == "oil" || c.Token == "milk" ||
                                              c.Token == "furniture" || c.Token == "machinery");
            bool allRealOk = cargoes.All(c =>
                c.Token.StartsWith("cargo.", StringComparison.Ordinal) ||
                c.Token.StartsWith("cargo_obsole.", StringComparison.Ordinal));
            Console.WriteLine($"[gamedata] cargoes={cargoes.Count} (selectable {selectableCount}, " +
                              $"names table {GameNames.Cargo.Count}) gummy='{gummy?.Name ?? "(null)"}'");
            if (!cargoCountOk)
            {
                Console.WriteLine("FAIL: the built-in cargo list does not match the extracted cargo table");
                failures++;
            }
            if (!cargoTokenOk)
            {
                Console.WriteLine("FAIL: the built-in cargo list does not use the real game tokens");
                failures++;
            }
            if (!noFakeOk)
            {
                Console.WriteLine("FAIL: the built-in cargo list still contains the fictional tokens");
                failures++;
            }
            if (!allRealOk)
            {
                Console.WriteLine("FAIL: the built-in cargo list contains tokens the game does not know");
                failures++;
            }

            // 4) 货运公司：来自公司显示名表。
            var companies = data.Companies();
            bool companyOk = companies.Count > 100 &&
                             companies.Any(c => c.Token == "company.permanent.nbfc" && c.Name == "NBFC");
            Console.WriteLine($"[gamedata] companies={companies.Count}");
            if (!companyOk)
            {
                Console.WriteLine("FAIL: the built-in company list does not use the extracted company names");
                failures++;
            }

            // 5) 「货物」页的 ViewModel：数据已装载，筛选可用。
            var vm = new CargoViewModel();
            bool vmOk = vm.Cities.Count == CityCatalogue.Count &&
                        vm.Cargos.Count == selectableCount;
            if (!vmOk)
            {
                Console.WriteLine("FAIL: the cargo view model did not load the catalogues");
                failures++;
            }

            vm.CityFilter = "伦敦";
            bool cityFilterOk = vm.Cities.Count == 1 && vm.Cities[0].Token == "london";
            vm.CityFilter = "";
            vm.CargoFilter = "Gummy";
            bool cargoFilterOk = vm.Cargos.Count == 1 && vm.Cargos[0].Token == "cargo.gummy_bears";
            vm.CargoFilter = "cargo.gummy";
            bool tokenFilterOk = vm.Cargos.Count >= 1 &&
                                 vm.Cargos.All(c => c.Token.IndexOf("cargo.gummy", StringComparison.OrdinalIgnoreCase) >= 0);
            vm.CargoFilter = "";
            Console.WriteLine($"[gamedata] filters: city={cityFilterOk} cargo={cargoFilterOk} token={tokenFilterOk}");
            if (!cityFilterOk || !cargoFilterOk || !tokenFilterOk)
            {
                Console.WriteLine("FAIL: the cargo view model filters do not work");
                failures++;
            }

            // 6) 已访问城市显示中文名 + token。
            if (File.Exists(RealSavePath))
            {
                string dir = CreateTempDir("gamedata");
                string copy = Path.Combine(dir, "gamedata_copy.sii");
                File.Copy(RealSavePath, copy, true);   // 只操作副本

                var sf = new SaveFile();
                sf.Load(copy);
                vm.LoadFrom(sf);

                bool visitedOk = vm.VisitedCities.Count > 0 &&
                                 vm.VisitedCities.All(v => v.Contains("(") && v.Contains(")"));
                Console.WriteLine($"[gamedata] visited cities={vm.VisitedCities.Count} " +
                                  $"sample='{vm.VisitedCities.FirstOrDefault() ?? "(none)"}'");
                if (!visitedOk)
                {
                    Console.WriteLine("FAIL: visited cities are not shown with their readable name");
                    failures++;
                }
            }

            return failures;
        }
    }
}
