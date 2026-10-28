using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;
using Ets2SaveEditor.Core.GameData;

namespace Ets2SaveEditor.ViewModels
{
    public partial class FreightMarketViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<JobOffer> _items = new();
        private SaveFile? _sf;

        public void LoadFrom(SaveFile sf) { _sf = sf; Refresh(); }
        public void ApplyToModel() { }

        private void Refresh() { if (_sf != null) Items = new ObservableCollection<JobOffer>(_sf.GetFreightJobs()); }

        [RelayCommand]
        private void CreateJob()
        {
            if (_sf?.Document == null) return;
            var cities = new BuiltInGameData().Cities();
            var cargos = new BuiltInGameData().Cargoes();
            var target = cities.Count > 0 ? cities[0].Token : "berlin";
            var cargo = cargos.Count > 0 ? cargos[0].Token : "cargo.haulie";
            JobOffer.Create(_sf.Document, null, target, cargo, string.Empty);
            Refresh();
        }

        [RelayCommand]
        private void DeleteJob(JobOffer? job)
        {
            if (_sf?.Document == null || job == null) return;
            _sf.Document.Units.Remove(job.Unit);
            Refresh();
        }
    }
}
