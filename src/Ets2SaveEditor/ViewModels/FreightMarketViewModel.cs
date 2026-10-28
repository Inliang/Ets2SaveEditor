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
        [ObservableProperty] private string _newCargo = "cargo.haulie";

        /// <summary>Company truck of a newly created job.</summary>
        [ObservableProperty] private string _newTruck = string.Empty;

        private SaveFile? _sf;

        public void LoadFrom(SaveFile sf)
        {
            _sf = sf;
            Refresh();
        }

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

            // Only companies that already publish at least one offer are
            // interesting for creating a new one; the game only shows
            // companies the player has discovered.
            var list = new List<FreightCompany>();
            foreach (var company in FreightCompany.AllFromDocument(_sf.Document))
            {
                if (company.JobOfferCount > 0) list.Add(company);
            }
            Companies = new ObservableCollection<FreightCompany>(ApplyCompanyFilter(list));

            if (SelectedCompany == null || !Companies.Contains(SelectedCompany))
            {
                SelectedCompany = Companies.FirstOrDefault();
            }

            Status = $"共 {TotalOfferCount} 条货运市场任务（显示前 {Items.Count} 条），{Companies.Count} 家公司";
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
                query = query.Where(c =>
                    (c.Id ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (c.CityName ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return query.OrderBy(c => c.CityName, StringComparer.CurrentCulture)
                         .ThenBy(c => c.Brand, StringComparer.CurrentCulture)
                         .ToList();
        }

        partial void OnCompanyFilterChanged(string value)
        {
            if (_sf != null) Refresh();
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

            string cargo = string.IsNullOrWhiteSpace(NewCargo) ? "cargo.haulie" : NewCargo.Trim();
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
