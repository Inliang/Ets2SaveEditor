using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.GameData;

namespace Ets2SaveEditor.ViewModels
{
    /// <summary>
    /// Backs the "货物" tab: shows the game's own city and cargo catalogues
    /// next to the cities the player has already visited.
    /// <para>
    /// 城市与货物都来自权威数据源（<see cref="CityCatalogue"/> 与
    /// <see cref="GameNames.Cargo"/>），与「城市」「货运市场」页签显示的
    /// 名称完全一致，不会再出现同一个城市两种语言的情况。
    /// </para>
    /// </summary>
    public partial class CargoViewModel : ObservableObject
    {
        /// <summary>完整的城市目录（未过滤）。</summary>
        private readonly List<City> _allCities = new List<City>();

        /// <summary>完整的货物目录（未过滤）。</summary>
        private readonly List<Cargo> _allCargos = new List<Cargo>();

        [ObservableProperty] private ObservableCollection<City> _cities = new();
        [ObservableProperty] private ObservableCollection<Cargo> _cargos = new();
        [ObservableProperty] private ObservableCollection<string> _visitedCities = new();

        /// <summary>城市列表的筛选词（按名称或 token 匹配）。</summary>
        [ObservableProperty] private string _cityFilter = string.Empty;

        /// <summary>货物列表的筛选词（按名称或 token 匹配）。</summary>
        [ObservableProperty] private string _cargoFilter = string.Empty;

        public CargoViewModel()
        {
            // 参考数据与存档无关：没加载存档时也应该能看。
            var data = new BuiltInGameData();
            _allCities.AddRange(data.Cities());
            _allCargos.AddRange(data.Cargoes());
            ApplyFilters();
        }

        public void LoadFrom(SaveFile sf)
        {
            var economy = sf?.GetEconomy();
            VisitedCities = new ObservableCollection<string>(
                FormatVisitedCities(economy?.VisitedCities));
        }

        public void ApplyToModel() { }

        partial void OnCityFilterChanged(string value) => ApplyFilters();

        partial void OnCargoFilterChanged(string value) => ApplyFilters();

        /// <summary>按当前的筛选词重建两个列表。</summary>
        private void ApplyFilters()
        {
            Cities = new ObservableCollection<City>(FilterCities(_allCities, CityFilter));
            Cargos = new ObservableCollection<Cargo>(FilterCargos(_allCargos, CargoFilter));
        }

        /// <summary>按名称或 token 过滤城市。</summary>
        private static List<City> FilterCities(IEnumerable<City> source, string filter)
        {
            string text = (filter ?? string.Empty).Trim();
            if (text.Length == 0) return source.ToList();
            return source.Where(c => Matches(c.Name, text) || Matches(c.Token, text)).ToList();
        }

        /// <summary>按名称或 token 过滤货物。</summary>
        private static List<Cargo> FilterCargos(IEnumerable<Cargo> source, string filter)
        {
            string text = (filter ?? string.Empty).Trim();
            if (text.Length == 0) return source.ToList();
            return source.Where(c => Matches(c.Name, text) || Matches(c.Token, text)).ToList();
        }

        /// <summary>不区分大小写的「包含」判断。</summary>
        private static bool Matches(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 已访问城市显示成「中文名 (token)」，与「城市」页签保持一致。
        /// </summary>
        private static List<string> FormatVisitedCities(IEnumerable<string>? tokens)
        {
            var list = new List<string>();
            if (tokens == null) return list;

            foreach (string token in tokens)
            {
                if (string.IsNullOrEmpty(token)) continue;

                string name = CityCatalogue.DisplayName(token);
                list.Add(string.Equals(name, token, StringComparison.Ordinal)
                    ? token
                    : name + " (" + token + ")");
            }

            return list;
        }
    }
}
