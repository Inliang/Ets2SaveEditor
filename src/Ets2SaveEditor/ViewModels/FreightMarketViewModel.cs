using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;
using FreightCompany = Ets2SaveEditor.Core.Domain.TypedUnits.Company;
using Ets2SaveEditor.Core.GameData;

namespace Ets2SaveEditor.ViewModels
{
    /// <summary>
    /// Backs the "货运市场" tab: shows the job offers of the freight market
    /// and lets the player create custom ones.
    /// <para>
    /// A job offer only shows up in the game when a company references it in
    /// its <c>job_offer</c> array, so creating a task also registers it with
    /// the chosen company.
    /// </para>
    /// </summary>
    public partial class FreightMarketViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<JobOffer> _items = new();
        [ObservableProperty] private ObservableCollection<FreightCompany> _companies = new();
        [ObservableProperty] private FreightCompany? _selectedCompany;
        [ObservableProperty] private string _status = "未加载存档";

        /// <summary>Filter for the company list of the creation form.</summary>
        [ObservableProperty] private string _companyFilter = string.Empty;

        /// <summary>
        /// Maximum number of offers shown at once. A real save holds more
        /// than five thousand offers; listing them all would make the table
        /// sluggish, so only the first ones are shown.
        /// </summary>
        [ObservableProperty] private int _maxShown = 500;

        /// <summary>Number of cargo units of a newly created job.</summary>
        [ObservableProperty] private int _newUnits = 24;

        /// <summary>Distance of a newly created job in kilometres.</summary>
        [ObservableProperty] private int _newDistance = 300;

        /// <summary>Urgency of a newly created job (0..3).</summary>
        [ObservableProperty] private int _newUrgency;

        /// <summary>Cargo of a newly created job.</summary>
        [ObservableProperty] private string _newCargo = DefaultCargo;

        /// <summary>Company truck of a newly created job.</summary>
        [ObservableProperty] private string _newTruck = string.Empty;

        /// <summary>
        /// Cargos offered in the creation form: the token plus its display
        /// name, so the user does not have to type game identifiers.
        /// <para>
        /// 内容来自静态的货物目录，只构建一次：如果每次刷新都重新 new 一份，
        /// 下拉框里的对象就换了新实例，用户已选的项会被判定成「不在列表里」
        /// 而被重置。
        /// </para>
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CargoChoice> _cargoChoices =
            new ObservableCollection<CargoChoice>(CargoCatalog);

        /// <summary>Currently selected cargo in the creation form.</summary>
        [ObservableProperty] private CargoChoice? _selectedCargo;

        /// <summary>
        /// 货物下拉框的内容（静态表，只构建一次）。
        /// </summary>
        private static readonly List<CargoChoice> CargoCatalog = BuildCargoChoices();

        /// <summary>
        /// 默认货物：下拉框里没选（或选了个无效的）时用它。
        /// </summary>
        private const string DefaultCargo = "cargo.haulie";

        /// <summary>
        /// 正在同步 <see cref="SelectedCargo"/> 与 <see cref="NewCargo"/>，
        /// 用来避免两个属性互相触发形成死循环。
        /// </summary>
        private bool _syncingCargo;

        /// <summary>当前选中的公司的 id（刷新后按 id 恢复选中项）。</summary>
        private string _selectedCompanyId = string.Empty;

        /// <summary>
        /// One entry of the cargo picker: the game token and the readable name.
        /// </summary>
        public sealed class CargoChoice
        {
            public CargoChoice(string token, string name)
            {
                Token = token;
                Name = name;
            }

            /// <summary>The game token written into the save (e.g. <c>cargo.haulie</c>).</summary>
            public string Token { get; }

            /// <summary>The readable name (e.g. <c>Haulier</c>).</summary>
            public string Name { get; }

            public override string ToString() => Name;
        }

        private SaveFile? _sf;

        public void LoadFrom(SaveFile sf)
        {
            _sf = sf;
            Refresh();
        }

        /// <summary>
        /// 新建任务的表单是「点了创建任务才生效」的：只有创建出来的任务才会
        /// 写进存档，因此这里不需要（也不能）把表单值回写到模型里。
        /// <para>
        /// 真正写存档的是 <see cref="CreateJobCommand"/>：它读取表单里的
        /// 货物 / 车辆 / 距离 / 数量 / 紧急度，写成新的 <c>job_offer_data</c>
        /// 单元，并注册到所选公司的 <c>job_offer</c> 数组里。
        /// </para>
        /// </summary>
        public void ApplyToModel() { }

        /// <summary>
        /// Rebuilds the offer list and the company list from the document.
        /// </summary>
        public void Refresh()
        {
            if (_sf == null)
            {
                Items = new ObservableCollection<JobOffer>();
                Companies = new ObservableCollection<FreightCompany>();
                Status = "未加载存档";
                return;
            }

            Items = new ObservableCollection<JobOffer>(Limit(_sf.GetFreightJobs()));

            // 货物下拉框：静态目录，只需确保它可用。
            EnsureCargoChoices();

            // 刷新后保留用户已选的货物（公司筛选变化也会触发刷新）。
            SyncCargoSelection();

            RefreshCompanies();
        }

        /// <summary>
        /// 重建公司列表（应用筛选词），并尽量保留当前选中的公司。
        /// <para>
        /// 公司列表与任务列表互不影响：改筛选词时只需要重建公司列表，
        /// 不必把几千条任务重新过滤一遍。
        /// </para>
        /// </summary>
        private void RefreshCompanies()
        {
            if (_sf == null)
            {
                Companies = new ObservableCollection<FreightCompany>();
                return;
            }

            // 先记住当前选中的公司：换了 Companies 之后，下拉框会清空选中项
            // （绑定是双向的，选中项会被回写成 null），事后再取就取不到了。
            string previousCompanyId = _selectedCompanyId;

            // Only companies that already publish at least one offer are
            // interesting for creating a new one; the game only shows
            // companies the player has discovered.
            var list = new List<FreightCompany>();
            foreach (var company in FreightCompany.AllFromDocument(_sf.Document))
            {
                if (company.JobOfferCount > 0) list.Add(company);
            }
            Companies = new ObservableCollection<FreightCompany>(ApplyCompanyFilter(list));

            // 公司列表每次刷新都是新的包装对象，只能按 id 恢复选中项；
            // 选中的公司被筛选掉了才退回列表第一项。
            SelectedCompany = Companies.FirstOrDefault(c =>
                                   string.Equals(c.Id, previousCompanyId, StringComparison.Ordinal))
                               ?? Companies.FirstOrDefault();

            UpdateStatus();
        }

        /// <summary>更新状态栏文字（任务总数与公司数量）。</summary>
        private void UpdateStatus()
        {
            if (_sf == null)
            {
                Status = "未加载存档";
                return;
            }

            Status = $"共 {TotalOfferCount} 条货运市场任务（显示前 {Items.Count} 条），{Companies.Count} 家公司";
        }

        /// <summary>
        /// 下拉框选中项 → 货物 token。
        /// </summary>
        partial void OnSelectedCargoChanged(CargoChoice? value)
        {
            if (_syncingCargo) return;

            string token = value?.Token ?? string.Empty;
            if (string.Equals(NewCargo, token, StringComparison.Ordinal)) return;

            _syncingCargo = true;
            try
            {
                NewCargo = token;
            }
            finally
            {
                _syncingCargo = false;
            }
        }

        /// <summary>
        /// 货物 token → 下拉框选中项。token 不在列表里时清空选中项。
        /// </summary>
        partial void OnNewCargoChanged(string value)
        {
            if (_syncingCargo) return;

            _syncingCargo = true;
            try
            {
                SyncCargoSelection();
            }
            finally
            {
                _syncingCargo = false;
            }
        }

        /// <summary>
        /// 记录当前选中的公司 id，供刷新后恢复选中项。
        /// </summary>
        partial void OnSelectedCompanyChanged(FreightCompany? value)
        {
            _selectedCompanyId = value?.Id ?? string.Empty;
        }

        /// <summary>
        /// Ensures the cargo picker holds the (static) cargo catalogue.
        /// </summary>
        private void EnsureCargoChoices()
        {
            if (CargoChoices != null && CargoChoices.Count == CargoCatalog.Count) return;
            CargoChoices = new ObservableCollection<CargoChoice>(CargoCatalog);
        }

        /// <summary>
        /// 让下拉框选中项与 <see cref="NewCargo"/> 保持一致：
        /// token 能对上就选中对应的项，对不上就清空选中项。
        /// </summary>
        private void SyncCargoSelection()
        {
            string token = (NewCargo ?? string.Empty).Trim();

            CargoChoice? match = null;
            foreach (var choice in CargoChoices)
            {
                if (string.Equals(choice.Token, token, StringComparison.OrdinalIgnoreCase))
                {
                    match = choice;
                    break;
                }
            }

            if (match != null)
            {
                if (!ReferenceEquals(SelectedCargo, match)) SelectedCargo = match;
            }
            else if (SelectedCargo != null)
            {
                SelectedCargo = null;
            }
        }

        /// <summary>
        /// Builds the cargo picker entries (token + readable name), sorted by
        /// the readable name.
        /// </summary>
        private static List<CargoChoice> BuildCargoChoices()
        {
            var cargoList = new List<CargoChoice>();
            foreach (var token in CargoCatalogue.Tokens)
            {
                string name = CargoCatalogue.DisplayName(token);
                if (string.IsNullOrEmpty(name)) continue;
                cargoList.Add(new CargoChoice(token, name));
            }
            cargoList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            return cargoList;
        }

        /// <summary>
        /// Total number of job offers in the save, regardless of how many
        /// are shown in the table.
        /// </summary>
        public int TotalOfferCount { get; private set; }

        /// <summary>
        /// Cuts the list down to the number of entries that should be shown.
        /// <para>
        /// Offers without a target and without a distance are placeholders the
        /// game keeps for every company slot (709 of them in a typical save);
        /// they carry no cargo and no payout, so they are left out.
        /// </para>
        /// </summary>
        private List<JobOffer> Limit(List<JobOffer> source)
        {
            var usable = new List<JobOffer>();
            foreach (var offer in source)
            {
                if (offer == null) continue;
                if (string.IsNullOrEmpty(offer.Target) || offer.DistanceKm <= 0) continue;
                usable.Add(offer);
            }

            TotalOfferCount = usable.Count;
            int max = MaxShown <= 0 ? usable.Count : MaxShown;
            return usable.Count <= max ? usable : usable.GetRange(0, max);
        }

        partial void OnMaxShownChanged(int value)
        {
            if (_sf != null) Refresh();
        }

        /// <summary>
        /// Applies the company filter to the given list.
        /// </summary>
        private List<FreightCompany> ApplyCompanyFilter(IEnumerable<FreightCompany> source)
        {
            IEnumerable<FreightCompany> query = source;

            string text = (CompanyFilter ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                query = query.Where(c => MatchesCompany(c, text));
            }

            return query.OrderBy(c => c.CityName, StringComparer.CurrentCulture)
                         .ThenBy(c => c.Brand, StringComparer.CurrentCulture)
                         .ToList();
        }

        /// <summary>
        /// 判断公司是否命中筛选词。
        /// <para>
        /// 界面上公司显示的是「公司名 · 城市」（例如 <c>NBFC · 阿伯丁</c>），
        /// 所以筛选必须同时匹配：显示名、品牌名、公司 id、城市名（中文）
        /// 以及城市 token。匹配一律忽略大小写。
        /// </para>
        /// </summary>
        private static bool MatchesCompany(FreightCompany company, string text)
        {
            if (company == null) return false;

            return Contains(company.DisplayName, text) ||
                   Contains(company.Brand, text) ||
                   Contains(company.Id, text) ||
                   Contains(company.CityName, text) ||
                   Contains(company.CityToken, text) ||
                   Contains(company.ToString(), text);
        }

        /// <summary>不区分大小写的「包含」判断。</summary>
        private static bool Contains(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        partial void OnCompanyFilterChanged(string value)
        {
            // 只重建公司列表：任务列表与筛选词无关，没必要重新过滤几千条任务。
            if (_sf != null) RefreshCompanies();
        }

        /// <summary>
        /// Creates a custom job offer and registers it with the selected
        /// company so that the game shows it in the freight market.
        /// </summary>
        [RelayCommand]
        private void CreateJob()
        {
            if (_sf?.Document == null)
            {
                Status = "请先加载存档。";
                return;
            }

            if (SelectedCompany == null)
            {
                Status = "请先选择一家收货公司。";
                return;
            }

            // 没在下拉框里选货物时退回默认货物，避免写出空的 cargo 引用。
            string cargo = ResolveCargoToken(NewCargo);
            string truck = (NewTruck ?? string.Empty).Trim();

            var job = JobOffer.CreateAndRegister(
                _sf.Document,
                SelectedCompany,
                cargo,
                truck,
                Math.Max(0, NewDistance),
                Math.Max(0, NewUnits),
                Math.Max(0, NewUrgency));

            if (job == null)
            {
                Status = "创建任务失败。";
                return;
            }

            Refresh();
            Status = $"已创建任务：{SelectedCompany.CityName}（{cargo}，{NewUnits} 单位，{NewDistance} km），"
                   + $"预计报酬 {job.EstimatedIncomeText}。保存存档后游戏即可看到该任务。";
        }

        /// <summary>
        /// 把货物 token 归一化：空值或货物目录里没有的 token 一律回落到默认
        /// 货物，避免把无效的 cargo 引用写进存档（游戏读不懂这种引用）。
        /// </summary>
        /// <param name="token">货物 token（可能为空或无效）。</param>
        /// <returns>货物目录里一定存在的 token。</returns>
        private string ResolveCargoToken(string token)
        {
            string trimmed = (token ?? string.Empty).Trim();
            if (trimmed.Length == 0) return DefaultCargo;

            foreach (var choice in CargoChoices)
            {
                if (string.Equals(choice.Token, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return choice.Token;
                }
            }

            return DefaultCargo;
        }

        /// <summary>
        /// Deletes the given job offer and unregisters it from every company.
        /// </summary>
        [RelayCommand]
        private void DeleteJob(JobOffer? job)
        {
            if (job == null) return;
            if (!job.Remove()) return;

            Refresh();
            Status = "已删除所选任务（并已从公司任务列表中注销）。";
        }
    }
}
