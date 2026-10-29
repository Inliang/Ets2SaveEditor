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

        /// <summary>Full identifier of the garage (e.g. <c>garage.calais</c>).</summary>
        public string Id => _unit != null ? _unit.Id : string.Empty;

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

        /// <summary>
        /// Readable name of the city the garage is in (e.g. <c>卡尔斯塔德</c>).
        /// </summary>
        public string CityName => GameData.CityCatalogue.DisplayName(City);

        /// <summary>
        /// Readable description of the garage status.
        /// <para>
        /// The game stores an internal flag; every garage of a real save
        /// carries <see cref="RegularStatus"/>. The value itself has no
        /// documented meaning, so it is shown as-is with a short hint.
        /// </para>
        /// </summary>
        public string StatusText
        {
            get
            {
                int status = Status;
                return status == RegularStatus
                    ? status + "（正常）"
                    : status.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Identifiers of the vehicles parked in this garage.
        /// <para>
        /// The game stores a fixed-size slot array in which unused slots hold
        /// <c>null</c>. Those placeholders are skipped here so the count
        /// reflects the vehicles that are actually there.
        /// </para>
        /// </summary>
        public List<string> Vehicles => GetIdList("vehicles");

        /// <summary>
        /// Identifiers of the drivers hired in this garage (unused slots hold
        /// <c>null</c> and are skipped).
        /// </summary>
        public List<string> Drivers => GetIdList("drivers");

        /// <summary>
        /// Identifiers of the trailers parked in this garage (unused slots hold
        /// <c>null</c> and are skipped).
        /// </summary>
        public List<string> Trailers => GetIdList("trailers");

        /// <summary>Number of vehicles parked in this garage.</summary>
        public int VehicleCount => Vehicles.Count;

        /// <summary>Number of drivers hired in this garage.</summary>
        public int DriverCount => Drivers.Count;

        /// <summary>Number of trailers parked in this garage.</summary>
        public int TrailerCount => Trailers.Count;

        /// <summary>
        /// Reads an array of unit references and drops the empty slots.
        /// <para>
        /// The game pads the arrays with <c>null</c> for unused slots — the
        /// placeholder is a reference whose target literally reads
        /// <c>"null"</c>. Keeping those would make every garage look like it
        /// owns five vehicles and five drivers.
        /// </para>
        /// </summary>
        private List<string> GetIdList(string attribute)
        {
            var result = new List<string>();
            if (_unit == null) return result;
            foreach (var item in _unit.GetArrayItems(attribute))
            {
                // AsReferenceId 会跳过 "null" 空位占位符。
                string id = item.AsReferenceId();
                if (string.IsNullOrEmpty(id)) continue;
                result.Add(id);
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
