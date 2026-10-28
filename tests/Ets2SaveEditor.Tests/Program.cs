using System;
using System.IO;
using Ets2SaveEditor.Core.Domain;
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

            Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} TEST(S) FAILED");
            return failures == 0 ? 0 : 1;
        }
    }
}
