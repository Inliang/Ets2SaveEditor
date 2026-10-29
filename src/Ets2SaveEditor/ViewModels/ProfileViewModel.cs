using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;

namespace Ets2SaveEditor.ViewModels
{
    /// <summary>
    /// 「玩家档案」页的视图模型。
    /// <para>
    /// <b>等级与经验值的数据流向</b>：游戏存档里只保存累计经验值
    /// （<c>economy.experience_points</c>），等级是游戏读档时按自己的曲线算出来的派生值。
    /// 因此本模型以<b>经验值为唯一数据源</b>，等级是可写回的派生视图：
    /// <list type="number">
    /// <item>改「经验值」→ 立即按 <c>LevelFromXp</c> 重新推导等级与升级进度；</item>
    /// <item>改「等级」→ 立即按 <c>XpForLevel</c> 换算成该等级的累计经验值写回经验值，
    /// 再由经验值反推等级。</item>
    /// </list>
    /// <para>
    /// <b>为什么不会「两条路径互相覆盖」</b>：等级→经验的换算是<b>即时的</b>（在 setter 里完成），
    /// 而不是推迟到存档时再决定谁优先。因此写回存档时只需写经验值这一个字段，
    /// 不存在写入顺序决定结果的歧义。
    /// </para>
    /// <para>
    /// 「设等级 = 设经验」这一语义与
    /// <see cref="Ets2SaveEditor.Core.Domain.TypedUnits.Player.Level"/> 的 setter 一致
    /// （等级 ≥ 1 时两者完全等价，都调用 <see cref="Player.XpForLevel"/>）。
    /// <para>
    /// <b>唯一差异</b>：等级 &lt; 1 时，<c>Player.Level</c> 会把经验清零
    /// （<c>Experience = XpForLevel(0) = 0</c>），而本模型<b>忽略该输入并回弹</b>到当前
    /// 经验值对应的等级——避免用户误输入 0 或负数就把进度清空。
    /// </para>
    /// </para>
    /// </para>
    /// </summary>
    public partial class ProfileViewModel : ObservableObject
    {
        [ObservableProperty] private long _money;

        /// <summary>累计经验值（存档里的真实字段，也是等级的唯一数据源）。</summary>
        [ObservableProperty] private long _experience;

        /// <summary>
        /// 当前等级：由 <see cref="Experience"/> 推导；写入时会把该等级换算成对应的累计经验值。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LevelText))]
        [NotifyPropertyChangedFor(nameof(LevelSummary))]
        private int _level;

        /// <summary>
        /// 存档中是否存在玩家数据。为 <c>false</c> 时等级与升级进度显示为「—」，
        /// 以此区分「没有玩家数据」与「真的是 1 级」——
        /// 游戏里不存在「0 级」这种存档状态。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LevelText))]
        [NotifyPropertyChangedFor(nameof(LevelSummary))]
        [NotifyPropertyChangedFor(nameof(LevelProgress))]
        private bool _hasPlayerProfile;

        /// <summary>距离下一级的提示文字（未加载存档显示“—”，满级显示“已满级”）。</summary>
        [ObservableProperty] private string _levelProgress = "—";

        [ObservableProperty] private int _longDist;
        [ObservableProperty] private int _heavy;
        [ObservableProperty] private int _fragile;
        [ObservableProperty] private int _urgent;
        [ObservableProperty] private int _ecoDriving;

        private Bank? _bank;
        private Player? _player;

        /// <summary>
        /// 等级与经验值互相换算时的重入保护：
        /// 由经验值推导等级时不得反写经验值，由等级换算经验值时不得再推导等级。
        /// </summary>
        private bool _syncing;

        /// <summary>
        /// 从存档载入档案数据。
        /// </summary>
        /// <param name="sf">已加载的存档。</param>
        public void LoadFrom(SaveFile sf)
        {
            _bank = sf.GetBank();
            _player = sf.GetPlayer();

            // 先置位「是否有玩家数据」，再赋经验值——赋值会触发等级与进度的重新推导。
            HasPlayerProfile = _player != null;

            Money = _bank?.Money ?? 0;
            Experience = _player?.Experience ?? 0;
            RefreshLevel(Experience);
            LongDist = Clamp(_player?.LongDist ?? 0);
            Heavy = Clamp(_player?.Heavy ?? 0);
            Fragile = Clamp(_player?.Fragile ?? 0);
            Urgent = Clamp(_player?.Urgent ?? 0);
            EcoDriving = Clamp(_player?.EcoDriving ?? 0);
        }

        /// <summary>
        /// 把界面上的数值写回存档。
        /// <para>
        /// 等级<b>不单独写回</b>：存档里只有经验值，等级是游戏读档时按
        /// <c>experience_points</c> 现算的派生值。用户在界面上改等级时，
        /// 等级已经被换算成经验值（<see cref="Level"/> 的 setter），
        /// 这里回写经验值即可，写档结果与游戏完全一致。
        /// </para>
        /// </summary>
        public void ApplyToModel()
        {
            if (_bank != null) _bank.Money = Money;
            if (_player != null)
            {
                // 等级在游戏里是经验值的派生结果，存档里只存经验值。
                // 因此这里只回写经验值——界面上改等级已经等价于改经验值，
                // 避免「改等级」与「改经验」两条路径互相覆盖。
                // 经验值下限为 0：游戏里的等级由经验值派生，负值会让游戏拒绝加载存档。
                _player.Experience = Math.Max(0, Experience);
                _player.LongDist = Clamp(LongDist);
                _player.Heavy = Clamp(Heavy);
                _player.Fragile = Clamp(Fragile);
                _player.Urgent = Clamp(Urgent);
                _player.EcoDriving = Clamp(EcoDriving);
            }
        }

        /// <summary>
        /// 经验值变化：由经验值重新推导等级，并刷新升级进度。
        /// </summary>
        /// <param name="value">新的累计经验值。</param>
        partial void OnExperienceChanged(long value)
        {
            if (_syncing) return;
            RefreshLevel(value);
        }

        /// <summary>
        /// 等级变化：立即换算成该等级的累计经验值（写回经验值），再由经验值反推等级。
        /// <para>
        /// 越界输入的处理：小于 1 的值被忽略（游戏里最低是 1 级，输入 0 或负数
        /// 不应该把玩家进度清零）；大于 <see cref="Player.MaxLevel"/> 的值按满级门槛封顶。
        /// </para>
        /// </summary>
        /// <param name="value">用户输入的等级。</param>
        partial void OnLevelChanged(int value)
        {
            if (_syncing) return;

            // 小于 1 级：忽略本次输入，保持当前进度不变（避免误输入清零经验值）。
            if (value < 1)
            {
                _syncing = true;
                try
                {
                    Level = Player.LevelFromXp(Experience);
                }
                finally
                {
                    _syncing = false;
                }

                return;
            }

            _syncing = true;
            try
            {
                // 等级 ≥ 1 时与 Player.Level 的 setter 同语义：设等级 = 设经验值。
                // XpForLevel 内部负责上界收敛（>MaxLevel → 满级门槛）。
                // 唯一的差异在等级 < 1：Player.Level 会清零经验值，而这里选择忽略输入，
                // 理由见上方类注释（防止误输入清空玩家进度）。
                long xp = Player.XpForLevel(value);
                if (Experience != xp) Experience = xp;

                // 归一化显示值：由经验值反推一次，保证界面显示与存档内容始终一致。
                Level = Player.LevelFromXp(Experience);
            }
            finally
            {
                _syncing = false;
            }

            RefreshLevelProgress();
        }

        /// <summary>
        /// 依据累计经验值刷新等级显示，并算出距离下一级还差多少经验。
        /// </summary>
        /// <param name="xp">累计经验值。</param>
        private void RefreshLevel(long xp)
        {
            _syncing = true;
            try
            {
                Level = Player.LevelFromXp(xp);
            }
            finally
            {
                _syncing = false;
            }

            RefreshLevelProgress();
        }

        /// <summary>刷新「距离下一级」的提示文字。</summary>
        private void RefreshLevelProgress()
        {
            if (!HasPlayerProfile)
            {
                LevelProgress = "—";
                return;
            }

            if (Level >= Player.MaxLevel)
            {
                LevelProgress = "已满级";
            }
            else
            {
                LevelProgress = $"还差 {Player.XpToNextLevel(Experience):N0} XP";
            }
        }

        /// <summary>
        /// 等级的数字文本（用于界面表格）；没有玩家数据时显示「—」。
        /// </summary>
        public string LevelText => HasPlayerProfile ? Level.ToString(CultureInfo.InvariantCulture) : "—";

        /// <summary>
        /// 等级的完整摘要（用于状态栏），例如「52 级」；没有玩家数据时显示「—」。
        /// </summary>
        public string LevelSummary => HasPlayerProfile ? $"{Level} 级" : "—";

        private static int Clamp(int v) => v < 0 ? 0 : (v > 6 ? 6 : v);
    }
}
