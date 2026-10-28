using CommunityToolkit.Mvvm.ComponentModel;
using Ets2SaveEditor.Core.Domain;
using Ets2SaveEditor.Core.Domain.TypedUnits;

namespace Ets2SaveEditor.ViewModels
{
    public partial class ProfileViewModel : ObservableObject
    {
        [ObservableProperty] private long _money;
        [ObservableProperty] private long _experience;
        [ObservableProperty] private int _level;
        [ObservableProperty] private int _longDist;
        [ObservableProperty] private int _heavy;
        [ObservableProperty] private int _fragile;
        [ObservableProperty] private int _urgent;
        [ObservableProperty] private int _ecoDriving;

        private Bank? _bank;
        private Player? _player;

        public void LoadFrom(SaveFile sf)
        {
            _bank = sf.GetBank();
            _player = sf.GetPlayer();
            Money = _bank?.Money ?? 0;
            Experience = _player?.Experience ?? 0;
            Level = _player != null ? Player.LevelFromXp(_player.Experience) : 0;
            LongDist = Clamp(_player?.LongDist ?? 0);
            Heavy = Clamp(_player?.Heavy ?? 0);
            Fragile = Clamp(_player?.Fragile ?? 0);
            Urgent = Clamp(_player?.Urgent ?? 0);
            EcoDriving = Clamp(_player?.EcoDriving ?? 0);
        }

        public void ApplyToModel()
        {
            if (_bank != null) _bank.Money = Money;
            if (_player != null)
            {
                _player.Experience = Experience;
                _player.LongDist = Clamp(LongDist);
                _player.Heavy = Clamp(Heavy);
                _player.Fragile = Clamp(Fragile);
                _player.Urgent = Clamp(Urgent);
                _player.EcoDriving = Clamp(EcoDriving);
            }
        }

        partial void OnExperienceChanged(long value)
        {
            Level = Player.LevelFromXp(value);
        }

        private static int Clamp(int v) => v < 0 ? 0 : (v > 6 ? 6 : v);
    }
}
