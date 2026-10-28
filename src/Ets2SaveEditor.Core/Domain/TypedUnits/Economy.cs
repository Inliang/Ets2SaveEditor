using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>economy</c> unit. The economy unit is the
    /// root of a save file: it references the bank, the player and all other
    /// sub-units and carries the global progress of the profile.
    /// </summary>
    public class Economy
    {
        private readonly SiiUnit _unit;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Economy(SiiUnit unit)
        {
            _unit = unit;
        }

        /// <summary>
        /// Cities the player has visited. Best-effort: reads the
        /// <c>visited_cities</c> attribute (array of encoded strings).
        /// Returns an empty list if the field is absent or its exact shape is
        /// unknown.
        /// </summary>
        public List<string> VisitedCities
        {
            get
            {
                var list = new List<string>();
                if (_unit == null) return list;

                foreach (var item in _unit.GetArrayItems(Constants.EconomyVisitedCitiesAttribute))
                {
                    string value = item.AsId();
                    if (!string.IsNullOrEmpty(value)) list.Add(value);
                }

                return list;
            }
        }

        /// <summary>Accumulated experience points (<c>experience_points</c>).</summary>
        public long ExperiencePoints
        {
            get { return _unit != null ? _unit.GetLong(Constants.EconomyExperienceAttribute) : 0; }
            set { if (_unit != null) _unit.SetScalar(Constants.EconomyExperienceAttribute, value); }
        }

        /// <summary>Total driven distance in kilometres (<c>total_distance</c>).</summary>
        public long TotalDistance
        {
            get { return _unit != null ? _unit.GetLong("total_distance") : 0; }
            set { if (_unit != null) _unit.SetScalar("total_distance", value); }
        }

        /// <summary>Total amount of fuel bought in litres (<c>total_fuel_litres</c>).</summary>
        public long TotalFuelLitres
        {
            get { return _unit != null ? _unit.GetLong("total_fuel_litres") : 0; }
            set { if (_unit != null) _unit.SetScalar("total_fuel_litres", value); }
        }

        /// <summary>Total amount of money spent on fuel (<c>total_fuel_price</c>).</summary>
        public long TotalFuelPrice
        {
            get { return _unit != null ? _unit.GetLong("total_fuel_price") : 0; }
            set { if (_unit != null) _unit.SetScalar("total_fuel_price", value); }
        }

        /// <summary>Average damage rate of the player (<c>adr</c>).</summary>
        public int Adr
        {
            get { return _unit != null ? (int)_unit.GetLong("adr") : 0; }
            set { if (_unit != null) _unit.SetScalar("adr", (long)value); }
        }

        /// <summary>Identifier of the bank unit referenced by the economy unit.</summary>
        public string BankId
        {
            get { return _unit != null ? _unit.GetReference("bank") : string.Empty; }
        }

        /// <summary>Identifier of the player unit referenced by the economy unit.</summary>
        public string PlayerId
        {
            get { return _unit != null ? _unit.GetReference("player") : string.Empty; }
        }

        /// <summary>Identifiers of all garages of the profile.</summary>
        public List<string> GarageIds
        {
            get
            {
                var list = new List<string>();
                if (_unit == null) return list;
                foreach (var item in _unit.GetArrayItems("garages"))
                {
                    list.Add(item.AsId());
                }
                return list;
            }
        }

        public static Economy FromDocument(SiiDocument doc)
        {
            if (doc == null) return null;
            var unit = doc.GetFirstByType(Constants.EconomyType);
            if (unit == null) return null;
            return new Economy(unit);
        }
    }
}
