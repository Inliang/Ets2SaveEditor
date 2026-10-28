using System.Collections.Generic;
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

        private void Refresh() { if (_sf != null) Items = new ObservableCollection<Vehicle>(_sf.GetVehicles()); }

        [RelayCommand]
        private void RepairAll() { foreach (var v in Items) v.Repair(); Refresh(); }

        [RelayCommand]
        private void RefuelAll() { foreach (var v in Items) v.Refuel(); Refresh(); }
    }
}
