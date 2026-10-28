using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>game</c> unit (experience, level, skills).
    /// The level is derived from experience using a built-in XP threshold table.
    /// </summary>
    public class Player
    {
        private readonly SiiUnit _unit;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Player(SiiUnit unit)
        {
            _unit = unit;
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

        /// <summary>Accumulated experience points.</summary>
        public long Experience
        {
            get { return _unit != null ? _unit.GetLong("experience") : 0; }
            set { if (_unit != null) _unit.SetScalar("experience", value); }
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
            return _unit != null ? (int)_unit.GetLong(name) : 0;
        }

        private void SetSkill(string name, int value)
        {
            if (_unit == null) return;
            int clamped = value < 0 ? 0 : (value > 6 ? 6 : value);
            _unit.SetScalar(name, (long)clamped);
        }

        public int LongDist { get { return GetSkill("long_dist"); } set { SetSkill("long_dist", value); } }
        public int Heavy { get { return GetSkill("heavy"); } set { SetSkill("heavy", value); } }
        public int Fragile { get { return GetSkill("fragile"); } set { SetSkill("fragile", value); } }
        public int Urgent { get { return GetSkill("urgent"); } set { SetSkill("urgent", value); } }
        public int EcoDriving { get { return GetSkill("ecodriving"); } set { SetSkill("ecodriving", value); } }

        /// <summary>Wraps the first <c>game</c> (falling back to <c>player</c>) unit.</summary>
        public static Player FromDocument(SiiDocument doc)
        {
            if (doc == null) return null;
            var unit = doc.GetFirstByType(Constants.GameType);
            if (unit == null) unit = doc.GetFirstByType(Constants.PlayerType);
            if (unit == null) return null;
            return new Player(unit);
        }
    }
}
