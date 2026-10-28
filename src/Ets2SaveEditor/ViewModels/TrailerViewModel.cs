using System.Collections.Generic;
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

        private void Refresh() { if (_sf != null) Items = new ObservableCollection<Trailer>(_sf.GetTrailers()); }

        [RelayCommand]
        private void RepairAll() { foreach (var t in Items) t.Repair(); Refresh(); }
    }
}
