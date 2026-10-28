using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>garage</c> unit.
    /// <para>
    /// The modern save format stores one <c>garage</c> unit per city. Each of
    /// them holds the parked vehicles (<c>vehicles</c>), the hired drivers
    /// (<c>drivers</c>), the parked trailers (<c>trailers</c>), a <c>status</c>
    /// flag, a reference to its <c>profit_log</c> and a <c>productivity</c>
    /// factor.
    /// </para>
    /// </summary>
    public class Garage
    {
        /// <summary>
        /// Status value the game uses for a regular (usable) garage. All
        /// garages of a real save file carry this value.
        /// </summary>
        public const int RegularStatus = 3;

        private readonly SiiUnit _unit;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Garage(SiiUnit unit)
        {
            _unit = unit;
        }

        /// <summary>Game internal status flag of the garage.</summary>
        public int Status
        {
            get { return _unit != null ? (int)_unit.GetLong("status") : 0; }
            set { if (_unit != null) _unit.SetScalar("status", (long)value); }
        }

        /// <summary>Productivity factor of the garage (<c>productivity</c>).</summary>
        public double Productivity
        {
            get { return _unit != null ? _unit.GetDouble("productivity") : 0; }
            set { if (_unit != null) _unit.SetScalar("productivity", value); }
        }

        /// <summary>Identifier of the profit log of this garage.</summary>
        public string ProfitLogId
        {
            get { return _unit != null ? _unit.GetReference("profit_log") : string.Empty; }
        }

        /// <summary>City the garage belongs to (taken from the unit id).</summary>
        public string City
        {
            get
            {
                if (_unit == null) return string.Empty;
                string id = _unit.Id;
                int index = id.LastIndexOf('.');
                return index >= 0 && index < id.Length - 1 ? id.Substring(index + 1) : id;
            }
        }

        /// <summary>Identifiers of the vehicles parked in this garage.</summary>
        public List<string> Vehicles => GetIdList("vehicles");

        /// <summary>Identifiers of the drivers hired in this garage.</summary>
        public List<string> Drivers => GetIdList("drivers");

        /// <summary>Identifiers of the trailers parked in this garage.</summary>
        public List<string> Trailers => GetIdList("trailers");

        /// <summary>Number of vehicles parked in this garage.</summary>
        public int VehicleCount => Vehicles.Count;

        /// <summary>Number of drivers hired in this garage.</summary>
        public int DriverCount => Drivers.Count;

        /// <summary>Number of trailers parked in this garage.</summary>
        public int TrailerCount => Trailers.Count;

        private List<string> GetIdList(string attribute)
        {
            var result = new List<string>();
            if (_unit == null) return result;
            foreach (var item in _unit.GetArrayItems(attribute))
            {
                result.Add(item.AsId());
            }
            return result;
        }

        /// <summary>
        /// Marks the garage as usable by applying the status value the game
        /// uses for regular garages.
        /// </summary>
        public void Buy()
        {
            Status = RegularStatus;
        }

        /// <summary>Resets the productivity factor of the garage.</summary>
        public void ResetProductivity()
        {
            Productivity = 0;
        }

        public static Garage FromDocument(SiiDocument doc, string id)
        {
            if (doc == null) return null;
            var unit = doc.GetUnitById(id);
            if (unit == null || unit.Type != Constants.GarageType) return null;
            return new Garage(unit);
        }

        public static List<Garage> AllFromDocument(SiiDocument doc)
        {
            var list = new List<Garage>();
            if (doc == null) return list;
            foreach (var u in doc.GetUnitsByType(Constants.GarageType))
            {
                list.Add(new Garage(u));
            }
            return list;
        }
    }
}
