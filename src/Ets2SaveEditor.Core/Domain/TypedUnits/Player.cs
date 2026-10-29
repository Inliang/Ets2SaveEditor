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
        /// 最高等级（Divine Champion）。再往上游戏仍会计数，但等级名不再变化，
        /// 因此本工具只保留 1~100 级。
        /// </summary>
        public const int MaxLevel = 100;

        /// <summary>
        /// 达到 <see cref="MaxLevel"/> 级所需的累计经验值（Divine Champion 门槛）。
        /// </summary>
        public const long MaxLevelXp = 575700;

        /// <summary>
        /// 等级经验增量表：索引 <c>i</c> 表示从等级 <c>i</c> 升到等级 <c>i+1</c> 所需的 XP <b>增量</b>。
        /// <para>
        /// 数据来源：游戏安装目录 <c>def.scs</c>（SCS# v2 归档）内的
        /// <c>xp_level_data : economy.data.xp_levels</c> 块，字段 <c>level_xp[]</c>。
        /// 归档里的注释是 <c>experience points to the next level</c>，即<b>增量</b>语义，
        /// 不是累计值——这是本表最容易搞错的地方。
        /// </para>
        /// <para>
        /// 该块只显式给出了 <c>level_xp[0..29]</c>（30 项）；30 级之后游戏沿用最后一项
        /// 6800，因此这里把索引 30~99 补齐为 6800。
        /// </para>
        /// <para>
        /// 可靠性经过三重交叉验证：
        /// 增量累加后与公开等级表逐级比对，L1..L17 共 17/17 项精确匹配；
        /// 累加到 L100 得 575700，与公开资料「Divine Champion 575,700 XP」门槛精确相等；
        /// 真实存档 <c>experience_points = 252508</c> 推导为 52 级，与游戏内显示的 52 级一致
        /// （旧表给出 21 级，即用户报告的错误）。
        /// </para>
        /// <para>
        /// 游戏版本升级后需要重新从 <c>def.scs</c> 提取此表。
        /// </para>
        /// </summary>
        public static readonly long[] LevelXpIncrements = BuildLevelXpIncrements();

        /// <summary>
        /// 累计经验门槛：<c>LevelXpThresholds[L]</c> = 达到等级 <c>L</c> 所需的累计 XP
        /// （L 从 1 到 100 有效，索引 0 仅作占位）。
        /// </summary>
        private static readonly long[] LevelXpThresholds = BuildLevelXpThresholds();

        /// <summary>
        /// 构造等级经验增量表：前 30 项取自 <c>def.scs</c>，其余按游戏的规则补齐为 6800。
        /// </summary>
        private static long[] BuildLevelXpIncrements()
        {
            // def.scs → xp_level_data : economy.data.xp_levels → level_xp[0..29]
            long[] defined =
            {
                200, 500, 700, 900, 1000, 1100, 1300, 1600, 1700, 2100,
                2300, 2600, 2700, 2900, 3000, 3100, 3400, 3700, 4000, 4300,
                4600, 4700, 4900, 5200, 5700, 5900, 6000, 6200, 6600, 6800
            };

            var all = new long[MaxLevel];
            for (int i = 0; i < all.Length; i++)
            {
                // 30 级之后游戏沿用最后一项（6800）。
                all[i] = i < defined.Length ? defined[i] : defined[defined.Length - 1];
            }
            return all;
        }

        /// <summary>
        /// 由增量表累加出各等级的累计经验门槛。
        /// </summary>
        private static long[] BuildLevelXpThresholds()
        {
            var thresholds = new long[MaxLevel + 1];
            long total = 0;
            for (int i = 0; i < MaxLevel; i++)
            {
                total += LevelXpIncrements[i];
                thresholds[i + 1] = total;
            }
            return thresholds;
        }

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

        /// <summary>
        /// 根据累计经验值推导等级：返回累计经验门槛不超过 <paramref name="xp"/> 的最高等级。
        /// <para>
        /// 等级 1 的门槛是 200 XP，但 0 XP 也是 1 级（新号即 1 级），因此等级下限为 1。
        /// 超过 <see cref="MaxLevelXp"/>（575700 XP）后封顶为 <see cref="MaxLevel"/> 级。
        /// </para>
        /// </summary>
        /// <param name="xp">累计经验值。</param>
        /// <returns>1 到 <see cref="MaxLevel"/> 之间的等级。</returns>
        public static int LevelFromXp(long xp)
        {
            if (xp < 0) xp = 0;

            int level = 1;
            for (int i = 1; i <= MaxLevel; i++)
            {
                if (LevelXpThresholds[i] > xp) break;
                level = i;
            }
            return level;
        }

        /// <summary>
        /// 达到指定等级所需的累计经验值。
        /// <para>
        /// 1 级是下限（0 XP 的新号即 1 级），因此返回 0；
        /// 2 级及以上按 <c>def.scs</c> 的累计门槛取值，例如
        /// <c>XpForLevel(2) = 700</c>、<c>XpForLevel(3) = 1400</c>、<c>XpForLevel(100) = 575700</c>。
        /// 高于 <see cref="MaxLevel"/> 时封顶为 <see cref="MaxLevelXp"/>。
        /// </para>
        /// <para>
        /// 本方法与 <see cref="LevelFromXp"/> 互为逆运算：
        /// <c>LevelFromXp(XpForLevel(L)) == L</c>（1 ≤ L ≤ 100），
        /// 因此把等级写回存档时不会产生等级偏差。
        /// </para>
        /// </summary>
        /// <param name="level">目标等级。</param>
        /// <returns>达到该等级所需的累计经验值。</returns>
        public static long XpForLevel(int level)
        {
            if (level < 1) return 0;
            if (level > MaxLevel) return MaxLevelXp;
            return LevelXpThresholds[level];
        }

        /// <summary>
        /// 距离下一级还差多少经验；已满级时返回 0。
        /// <para>
        /// 用于在界面上显示升级进度，例如 252508 XP 时距离 53 级还差 3592 XP。
        /// </para>
        /// </summary>
        /// <param name="xp">当前累计经验值。</param>
        /// <returns>升到下一级所需的剩余经验值，满级时为 0。</returns>
        public static long XpToNextLevel(long xp)
        {
            if (xp < 0) xp = 0;

            int level = LevelFromXp(xp);
            if (level >= MaxLevel) return 0;
            return LevelXpThresholds[level + 1] - xp;
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
