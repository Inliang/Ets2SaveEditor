using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;

namespace Ets2SaveEditor.ViewModels
{
    public partial class GaragesViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Garage> _items = new();
        private SaveFile? _sf;

        public void LoadFrom(SaveFile sf) { _sf = sf; Refresh(); }
        public void ApplyToModel() { }

        private void Refresh() { if (_sf != null) Items = new ObservableCollection<Garage>(_sf.GetGarages()); }

        [RelayCommand]
        private void Buy(Garage? g) { g?.Buy(); Refresh(); }

        [RelayCommand]
        private void ResetProductivity(Garage? g) { g?.ResetProductivity(); Refresh(); }
    }
}
