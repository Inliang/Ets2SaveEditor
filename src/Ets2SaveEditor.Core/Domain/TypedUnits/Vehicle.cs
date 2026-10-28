using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>vehicle</c> unit (wear, fuel).
    /// </summary>
    public class Vehicle
    {
        private readonly SiiUnit _unit;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Vehicle(SiiUnit unit)
        {
            _unit = unit;
        }

        public double EngineWear { get { return GetWear("wear_engine"); } set { SetWear("wear_engine", value); } }
        public double ChassisWear { get { return GetWear("wear_chassis"); } set { SetWear("wear_chassis", value); } }
        public double TransmissionWear { get { return GetWear("wear_transmission"); } set { SetWear("wear_transmission", value); } }
        public double CabinWear { get { return GetWear("wear_cabin"); } set { SetWear("wear_cabin", value); } }
        public double WheelWear { get { return GetWear("wear_wheels"); } set { SetWear("wear_wheels", value); } }

        private double GetWear(string name)
        {
            return _unit != null ? _unit.GetDouble(name) : 0;
        }

        private void SetWear(string name, double value)
        {
            if (_unit == null) return;
            double v = value < 0 ? 0 : (value > 1 ? 1 : value);
            _unit.SetScalar(name, v);
        }

        public double Fuel
        {
            get { return _unit != null ? _unit.GetDouble("fuel") : 0; }
            set { if (_unit != null) _unit.SetScalar("fuel", value); }
        }

        public double FuelCapacity
        {
            get { return _unit != null ? _unit.GetDouble("fuel_capacity") : 0; }
            set { if (_unit != null) _unit.SetScalar("fuel_capacity", value); }
        }

        /// <summary>Resets all wear values to 0 (brand new).</summary>
        public void Repair()
        {
            EngineWear = 0;
            ChassisWear = 0;
            TransmissionWear = 0;
            CabinWear = 0;
            WheelWear = 0;
        }

        /// <summary>Fills the tank up to its capacity (no-op if capacity is unknown).</summary>
        public void Refuel()
        {
            if (_unit == null) return;
            Fuel = FuelCapacity > 0 ? FuelCapacity : Fuel;
        }

        public static List<Vehicle> AllFromDocument(SiiDocument doc)
        {
            var list = new List<Vehicle>();
            if (doc == null) return list;
            foreach (var u in doc.GetUnitsByType(Constants.VehicleType))
            {
                list.Add(new Vehicle(u));
            }
            return list;
        }
    }
}
