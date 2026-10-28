using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;
using Ets2SaveEditor.Core.GameData;

namespace Ets2SaveEditor.ViewModels
{
    /// <summary>
    /// Backs the "城市" tab: lists every known city and lets the player
    /// unlock (visit) the ones they have not discovered yet.
    /// </summary>
    public partial class CitiesViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<VisitedCity> _items = new();
        [ObservableProperty] private string _filter = string.Empty;
        [ObservableProperty] private bool _showOnlyUnvisited;
        [ObservableProperty] private string _status = "未加载存档";

        private Economy? _economy;

        public void LoadFrom(SaveFile sf)
        {
            _economy = sf.GetEconomy();
            Refresh();
        }

        public void ApplyToModel()
        {
            // 城市访问直接作用于模型（VisitCity 会立即写入 SII 单元），
            // 因此保存时无需额外回写。
        }

        /// <summary>
        /// Rebuilds the list from the catalogue and marks the cities the
        /// player has already visited.
        /// </summary>
        public void Refresh()
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_economy != null)
            {
                foreach (string token in _economy.VisitedCities)
                {
                    visited.Add(token);
                }
            }

            var list = new List<VisitedCity>();
            foreach (var city in CityCatalogue.Cities())
            {
                list.Add(new VisitedCity(city.Token, city.Name, visited.Contains(city.Token)));
            }

            // 存档中出现过但不在目录里的城市也要显示出来。
            foreach (string token in visited)
            {
                if (CityCatalogue.Find(token) == null)
                {
                    list.Add(new VisitedCity(token, token, true));
                }
            }

            Items = new ObservableCollection<VisitedCity>(ApplyFilter(list));

            int visitedCount = list.Count(c => c.Visited);
            Status = _economy == null
                ? "未加载存档"
                : $"已访问 {visitedCount} / {list.Count} 个城市";
        }

        /// <summary>
        /// Applies the current filter to the given list.
        /// </summary>
        private List<VisitedCity> ApplyFilter(IEnumerable<VisitedCity> source)
        {
            IEnumerable<VisitedCity> query = source;

            if (ShowOnlyUnvisited)
            {
                query = query.Where(c => !c.Visited);
            }

            string text = (Filter ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                query = query.Where(c =>
                    (c.Token ?? string.Empty).IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (c.Name ?? string.Empty).IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return query.OrderBy(c => c.Visited).ThenBy(c => c.Name, System.StringComparer.CurrentCulture).ToList();
        }

        partial void OnFilterChanged(string value)
        {
            if (_economy != null) Refresh();
        }

        partial void OnShowOnlyUnvisitedChanged(bool value)
        {
            if (_economy != null) Refresh();
        }

        /// <summary>
        /// Unlocks the given city (adds it to <c>economy.visited_cities</c>).
        /// </summary>
        [RelayCommand]
        private void Visit(VisitedCity? city)
        {
            if (city == null || _economy == null) return;
            if (_economy.VisitCity(city.Token))
            {
                city.Visited = true;
                Status = $"已解锁城市：{city.DisplayName}";
            }
            else
            {
                Status = $"城市「{city.DisplayName}」已经访问过了。";
            }
            Refresh();
        }

        /// <summary>
        /// Unlocks every city that is currently filtered.
        /// </summary>
        [RelayCommand]
        private void VisitAll()
        {
            if (_economy == null) return;

            int added = 0;
            foreach (var city in Items.ToList())
            {
                if (city.Visited) continue;
                if (_economy.VisitCity(city.Token)) added++;
            }

            Status = added > 0
                ? $"已解锁 {added} 个城市。"
                : "没有需要解锁的城市。";
            Refresh();
        }

        /// <summary>
        /// Removes the given city from the visited list.
        /// </summary>
        [RelayCommand]
        private void Unvisit(VisitedCity? city)
        {
            if (city == null || _economy == null) return;
            if (_economy.UnvisitCity(city.Token))
            {
                city.Visited = false;
                Status = $"已取消访问：{city.DisplayName}";
            }
            else
            {
                Status = $"未能取消访问「{city.DisplayName}」。";
            }
            Refresh();
        }
    }
}
