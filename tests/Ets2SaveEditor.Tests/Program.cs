using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Sii;
using Ets2SaveEditor.ViewModels;

namespace Ets2SaveEditor.Tests
{
    public static class Program
    {
        public static int Main()
        {
            int failures = 0;

            var sample =
                "SiiNunit\n{\n" +
                "bank : _nameless.0001 {\n bank : 100000\n}\n" +
                "garage : _nameless.0002 {\n status : 0\n location : berlin\n upgrade : 0\n}\n" +
                "vehicle : _nameless.0003 {\n wear_engine : 0.7\n fuel : 20.0\n fuel_capacity : 100.0\n}\n" +
                "game : _nameless.0000 {\n experience : 5000\n long_dist : 2\n heavy : 1\n}\n" +
                "job_offer_data : _nameless.0004 {\n source_city : berlin\n target_city : paris\n cargo : oil\n reward : 5000\n}\n}\n";

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
            bool garageOk = g2.Items.Count == gcount && g2.Items[0].Status == 1;
            bool wearOk = t2.Items.Count > 0 && t2.Items[0].EngineWear == 0 && t2.Items[0].Fuel == 100.0;
            bool jobOk = f2.Items.Count == jcount + 1;

            Console.WriteLine($"moneyOk={moneyOk} xpOk={xpOk} garageOk={garageOk} wearOk={wearOk} jobOk={jobOk}");
            if (!moneyOk) { Console.WriteLine("FAIL: money not persisted"); failures++; }
            if (!xpOk) { Console.WriteLine("FAIL: experience not persisted"); failures++; }
            if (!garageOk) { Console.WriteLine("FAIL: garage buy not persisted"); failures++; }
            if (!wearOk) { Console.WriteLine("FAIL: repair/refuel not persisted"); failures++; }
            if (!jobOk) { Console.WriteLine("FAIL: new job not persisted"); failures++; }

            failures += TestEncrypted();

            Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>
        /// 构造一个 SrcC（AES-256-CBC + zlib）加密存档并验证可被正确加载解码。
        /// 这是修复“无法加载存档”的核心回归测试。
        /// </summary>
        private static int TestEncrypted()
        {
            int failures = 0;

            string plaintext =
                "SiiNunit\n{\n" +
                "bank : _nameless.0001 {\n bank : 250000\n}\n" +
                "garage : _nameless.0002 {\n status : 1\n location : paris\n}\n" +
                "}\n";

            // 1) zlib 压缩明文
            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                {
                    var data = Encoding.UTF8.GetBytes(plaintext);
                    z.Write(data, 0, data.Length);
                }
                compressed = ms.ToArray();
            }

            // 2) AES-256-CBC 加密（与解码端使用相同的密钥/IV）
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

            // 3) 组装 SrcC 容器
            byte[] container = new byte[56 + cipher.Length];
            byte[] sig = { 0x53, 0x72, 0x63, 0x43 }; // "SrcC"
            Array.Copy(sig, 0, container, 0, 4);
            Array.Copy(iv, 0, container, 36, 16);
            Array.Copy(cipher, 0, container, 56, cipher.Length);

            string dir = Path.Combine(Path.GetTempPath(), "ets2test");
            Directory.CreateDirectory(dir);
            string encPath = Path.Combine(dir, "encrypted.sii");
            File.WriteAllBytes(encPath, container);

            // 4) 加载并校验
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
            bool parsedOk = sf.Document != null && sf.Document.Units.Count >= 2;
            var bank = sf.GetBank();
            bool moneyOk = bank != null && bank.Money == 250000;

            Console.WriteLine($"[encrypted] formatOk={formatOk} parsedOk={parsedOk} moneyOk={moneyOk} units={sf.Document?.Units.Count}");
            if (!formatOk) { Console.WriteLine("FAIL: encrypted format not detected"); failures++; }
            if (!parsedOk) { Console.WriteLine("FAIL: encrypted save not parsed"); failures++; }
            if (!moneyOk) { Console.WriteLine("FAIL: bank money not decoded from encrypted save"); failures++; }

            return failures;
        }
    }
}
