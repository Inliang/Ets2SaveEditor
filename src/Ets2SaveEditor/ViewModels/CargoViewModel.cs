using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.GameData;

namespace Ets2SaveEditor.ViewModels
{
    public partial class CargoViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<City> _cities = new();
        [ObservableProperty] private ObservableCollection<Cargo> _cargos = new();
        [ObservableProperty] private ObservableCollection<string> _visitedCities = new();

        public void LoadFrom(SaveFile sf)
        {
            var data = new BuiltInGameData();
            Cities = new ObservableCollection<City>(data.Cities());
            Cargos = new ObservableCollection<Cargo>(data.Cargoes());
            var economy = sf.GetEconomy();
            VisitedCities = new ObservableCollection<string>(economy?.VisitedCities ?? new List<string>());
        }

        public void ApplyToModel() { }
    }
}
