using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>garage</c> unit.
    /// </summary>
    public class Garage
    {
        private readonly SiiUnit _unit;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Garage(SiiUnit unit)
        {
            _unit = unit;
        }

        public int Status
        {
            get { return _unit != null ? (int)_unit.GetLong("status") : 0; }
            set { if (_unit != null) _unit.SetScalar("status", (long)value); }
        }

        public string Location
        {
            get { return _unit != null ? _unit.GetString("location") : string.Empty; }
            set { if (_unit != null) _unit.SetScalar("location", value); }
        }

        public int Upgrade
        {
            get { return _unit != null ? (int)_unit.GetLong("upgrade") : 0; }
            set { if (_unit != null) _unit.SetScalar("upgrade", (long)value); }
        }

        /// <summary>
        /// Marks the garage as owned. NOTE: the exact "owned" value is pending
        /// verification against real ETS2 saves; 1 is used as a reasonable default.
        /// </summary>
        public void Buy()
        {
            Status = 1;
        }

        public void UpgradeTo(int lvl)
        {
            Upgrade = lvl < 0 ? 0 : lvl;
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
