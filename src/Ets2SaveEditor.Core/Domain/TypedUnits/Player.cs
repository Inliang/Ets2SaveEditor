using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the player related units of the modern save format.
    /// <para>
    /// The experience points and the driving skills live in the <c>economy</c>
    /// unit (<c>experience_points</c>, <c>long_dist</c>, <c>heavy</c>,
    /// <c>fragile</c>, <c>urgent</c>, <c>ecodriving</c>), while the current
    /// player state (home city, current job, owned vehicles…) is stored in the
    /// <c>player</c> unit.
    /// </para>
    /// </summary>
    public class Player
    {
        private readonly SiiUnit _economy;
        private readonly SiiUnit _player;

        /// <summary>The <c>economy</c> unit holding experience and skills.</summary>
        public SiiUnit EconomyUnit => _economy;

        /// <summary>The <c>player</c> unit holding the current player state.</summary>
        public SiiUnit Unit => _player;

        public Player(SiiUnit economyUnit, SiiUnit playerUnit)
        {
            _economy = economyUnit;
            _player = playerUnit;
        }

        /// <summary>
        /// XP thresholds per level (index = level). Index 0 has no XP requirement.
        /// Values increase monotonically; extends to roughly level 40.
        /// </summary>
        public static readonly long[] XpTable = new long[]
        {
            0, 2000, 5000, 9000, 14000, 20000, 27000, 35000, 44000, 54000,
            65000, 77000, 90000, 104000, 119000, 135000, 152000, 170000, 189000, 209000,
            230000, 252000, 275000, 299000, 324000, 350000, 377000, 405000, 434000, 464000,
            495000, 527000, 560000, 594000, 629000, 665000, 702000, 740000, 779000, 819000
        };

        /// <summary>Accumulated experience points (<c>economy.experience_points</c>).</summary>
        public long Experience
        {
            get { return _economy != null ? _economy.GetLong(Constants.EconomyExperienceAttribute) : 0; }
            set { if (_economy != null) _economy.SetScalar(Constants.EconomyExperienceAttribute, value); }
        }

        /// <summary>Player level, derived from <see cref="Experience"/>.</summary>
        public int Level
        {
            get { return LevelFromXp(Experience); }
            set { Experience = XpForLevel(value); }
        }

        /// <summary>Returns the highest level whose threshold is &lt;= xp.</summary>
        public static int LevelFromXp(long xp)
        {
            int level = 0;
            for (int i = 0; i < XpTable.Length; i++)
            {
                if (xp >= XpTable[i]) level = i;
                else break;
            }
            return level;
        }

        /// <summary>Returns the XP required for the given level (clamped to table bounds).</summary>
        public static long XpForLevel(int level)
        {
            int idx = level < 0 ? 0 : (level >= XpTable.Length ? XpTable.Length - 1 : level);
            return XpTable[idx];
        }

        private int GetSkill(string name)
        {
            return _economy != null ? (int)_economy.GetLong(name) : 0;
        }

        private void SetSkill(string name, int value)
        {
            if (_economy == null) return;
            int clamped = value < 0 ? 0 : (value > 6 ? 6 : value);
            _economy.SetScalar(name, (long)clamped);
        }

        public int LongDist { get { return GetSkill("long_dist"); } set { SetSkill("long_dist", value); } }
        public int Heavy { get { return GetSkill("heavy"); } set { SetSkill("heavy", value); } }
        public int Fragile { get { return GetSkill("fragile"); } set { SetSkill("fragile", value); } }
        public int Urgent { get { return GetSkill("urgent"); } set { SetSkill("urgent", value); } }
        public int EcoDriving { get { return GetSkill("ecodriving"); } set { SetSkill("ecodriving", value); } }

        /// <summary>Home city of the player (<c>player.hq_city</c>).</summary>
        public string HqCity
        {
            get { return _player != null ? _player.GetString("hq_city") : string.Empty; }
            set { if (_player != null) _player.SetScalarPreserveStyle("hq_city", value); }
        }

        /// <summary>Identifier of the job the player is currently running.</summary>
        public string CurrentJobId
        {
            get { return _player != null ? _player.GetReference("current_job") : string.Empty; }
        }

        /// <summary>Total driven distance (<c>economy.total_distance</c>, in km).</summary>
        public long TotalDistance
        {
            get { return _economy != null ? _economy.GetLong("total_distance") : 0; }
            set { if (_economy != null) _economy.SetScalar("total_distance", value); }
        }

        /// <summary>Total amount of fuel bought (<c>economy.total_fuel_litres</c>).</summary>
        public long TotalFuelLitres
        {
            get { return _economy != null ? _economy.GetLong("total_fuel_litres") : 0; }
            set { if (_economy != null) _economy.SetScalar("total_fuel_litres", value); }
        }

        /// <summary>Identifiers of the trucks owned by the player.</summary>
        public List<string> OwnedTrucks
        {
            get { return GetIdList(_player, "trucks"); }
        }

        /// <summary>Identifiers of the drivers owned by the player.</summary>
        public List<string> Drivers
        {
            get { return GetIdList(_player, "drivers"); }
        }

        private static List<string> GetIdList(SiiUnit unit, string attribute)
        {
            var result = new List<string>();
            if (unit == null) return result;
            foreach (var item in unit.GetArrayItems(attribute))
            {
                result.Add(item.AsId());
            }
            return result;
        }

        /// <summary>
        /// Wraps the <c>economy</c> and <c>player</c> units of the document.
        /// </summary>
        public static Player FromDocument(SiiDocument doc)
        {
            if (doc == null) return null;

            var economy = doc.GetFirstByType(Constants.EconomyType);
            var player = doc.GetFirstByType(Constants.PlayerType);
            if (economy == null && player == null) return null;

            // 兼容旧版存档：部分存档把经验放在 game 单元中。
            if (economy == null)
            {
                economy = doc.GetFirstByType(Constants.GameType);
            }

            return new Player(economy, player);
        }
    }
}
