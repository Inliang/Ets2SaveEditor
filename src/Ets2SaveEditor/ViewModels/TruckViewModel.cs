using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;

namespace Ets2SaveEditor.ViewModels
{
    public partial class TruckViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Vehicle> _items = new();
        private SaveFile? _sf;

        public void LoadFrom(SaveFile sf) { _sf = sf; Refresh(); }
        public void ApplyToModel() { }

        private void Refresh()
        {
            if (_sf == null) { Items = new ObservableCollection<Vehicle>(); return; }

            var all = _sf.GetVehicles();
            if (OnlyInGarage)
            {
                // 存档里会保留玩家卖掉的车对应的 vehicle 单元，
                // 只有还停在车库车位上的才算「拥有」。
                var owned = new System.Collections.Generic.List<Vehicle>();
                foreach (var v in all) if (v.IsInGarage) owned.Add(v);
                Items = new ObservableCollection<Vehicle>(owned);
            }
            else
            {
                Items = new ObservableCollection<Vehicle>(all);
            }

            OwnedCount = 0;
            foreach (var v in all) if (v.IsInGarage) OwnedCount++;
        }

        /// <summary>
        /// 只显示当前停在车库里的卡车（存档里还留着已售出车辆的历史单元）。
        /// </summary>
        [ObservableProperty] private bool _onlyInGarage;

        /// <summary>实际停在车库里的卡车数量。</summary>
        [ObservableProperty] private int _ownedCount;

        partial void OnOnlyInGarageChanged(bool value) => Refresh();

        /// <summary>修理全部卡车。</summary>
        [RelayCommand]
        private void RepairAll() { foreach (var v in Items) v.Repair(); Refresh(); }

        /// <summary>给全部卡车加满油。</summary>
        [RelayCommand]
        private void RefuelAll() { foreach (var v in Items) v.Refuel(); Refresh(); }

        /// <summary>只修理指定的一辆车。</summary>
        [RelayCommand]
        private void RepairOne(Vehicle v) { if (v == null) return; v.Repair(); Refresh(); }

        /// <summary>只给指定的一辆车加满油。</summary>
        [RelayCommand]
        private void RefuelOne(Vehicle v) { if (v == null) return; v.Refuel(); Refresh(); }
    }
}
