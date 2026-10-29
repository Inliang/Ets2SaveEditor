using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;

namespace Ets2SaveEditor.ViewModels
{
    public partial class TrailerViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Trailer> _items = new();
        private SaveFile? _sf;

        public void LoadFrom(SaveFile sf) { _sf = sf; Refresh(); }
        public void ApplyToModel() { }

        private void Refresh()
        {
            if (_sf == null) { Items = new ObservableCollection<Trailer>(); return; }

            var all = _sf.GetTrailers();
            if (OnlyInGarage)
            {
                // 存档里会保留玩家卖掉/换掉的挂车单元，只显示还停在车库的。
                var owned = new System.Collections.Generic.List<Trailer>();
                foreach (var t in all) if (t.IsInGarage) owned.Add(t);
                Items = new ObservableCollection<Trailer>(owned);
            }
            else
            {
                Items = new ObservableCollection<Trailer>(all);
            }
        }

        /// <summary>
        /// 只显示当前停在车库里的挂车（存档里还留着已处理挂车的历史单元）。
        /// </summary>
        [ObservableProperty] private bool _onlyInGarage;

        partial void OnOnlyInGarageChanged(bool value) => Refresh();

        /// <summary>修理全部挂车。</summary>
        [RelayCommand]
        private void RepairAll() { foreach (var t in Items) t.Repair(); Refresh(); }

        /// <summary>只修理指定的一辆挂车。</summary>
        [RelayCommand]
        private void RepairOne(Trailer t) { if (t == null) return; t.Repair(); Refresh(); }
    }
}
