using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>trailer</c> unit.
    /// </summary>
    public class Trailer
    {
        private readonly SiiUnit _unit;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Trailer(SiiUnit unit)
        {
            _unit = unit;
        }

        public double Wear
        {
            get { return _unit != null ? _unit.GetDouble("wear") : 0; }
            set { if (_unit != null) _unit.SetScalar("wear", ClampWear(value)); }
        }

        private static double ClampWear(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        /// <summary>
        /// Resets wear. Clears the aggregate "wear" field and any per-part wear
        /// keys that are present in the unit.
        /// </summary>
        public void Repair()
        {
            if (_unit == null) return;
            _unit.SetScalar("wear", 0.0);
            string[] keys = { "wear_chassis", "wear_wheels", "wear_body", "wear_cabin", "wear_axle" };
            foreach (var k in keys)
            {
                if (_unit.GetAttribute(k) != null) _unit.SetScalar(k, 0.0);
            }
        }

        public static List<Trailer> AllFromDocument(SiiDocument doc)
        {
            var list = new List<Trailer>();
            if (doc == null) return list;
            foreach (var u in doc.GetUnitsByType(Constants.TrailerType))
            {
                list.Add(new Trailer(u));
            }
            return list;
        }
    }
}
