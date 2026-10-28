using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>vehicle</c> unit.
    /// <para>
    /// The modern save format stores the wear of a truck in several separate
    /// float attributes (<c>engine_wear</c>, <c>transmission_wear</c>,
    /// <c>cabin_wear</c>, <c>chassis_wear</c> and the <c>wheels_wear</c>
    /// array) plus the unfixable part of each of them. The remaining fuel is
    /// stored as a relative value in <c>fuel_relative</c>.
    /// </para>
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

        /// <summary>Engine wear (0..1).</summary>
        public double EngineWear { get { return GetWear("engine_wear"); } set { SetWear("engine_wear", value); } }

        /// <summary>Transmission wear (0..1).</summary>
        public double TransmissionWear { get { return GetWear("transmission_wear"); } set { SetWear("transmission_wear", value); } }

        /// <summary>Cabin wear (0..1).</summary>
        public double CabinWear { get { return GetWear("cabin_wear"); } set { SetWear("cabin_wear", value); } }

        /// <summary>Chassis wear (0..1).</summary>
        public double ChassisWear { get { return GetWear("chassis_wear"); } set { SetWear("chassis_wear", value); } }

        /// <summary>
        /// Average wheel wear (0..1). The game stores one value per wheel in
        /// the <c>wheels_wear</c> array.
        /// </summary>
        public double WheelWear
        {
            get
            {
                var items = GetWearArray("wheels_wear");
                if (items.Count == 0) return 0;
                double sum = 0;
                foreach (var item in items) sum += item.AsDouble();
                return sum / items.Count;
            }
            set { SetWearArray("wheels_wear", value); }
        }

        /// <summary>Engine wear that cannot be repaired (0..1).</summary>
        public double EngineWearUnfixable { get { return GetWear("engine_wear_unfixable"); } set { SetWear("engine_wear_unfixable", value); } }

        /// <summary>Transmission wear that cannot be repaired (0..1).</summary>
        public double TransmissionWearUnfixable { get { return GetWear("transmission_wear_unfixable"); } set { SetWear("transmission_wear_unfixable", value); } }

        /// <summary>Cabin wear that cannot be repaired (0..1).</summary>
        public double CabinWearUnfixable { get { return GetWear("cabin_wear_unfixable"); } set { SetWear("cabin_wear_unfixable", value); } }

        /// <summary>Chassis wear that cannot be repaired (0..1).</summary>
        public double ChassisWearUnfixable { get { return GetWear("chassis_wear_unfixable"); } set { SetWear("chassis_wear_unfixable", value); } }

        /// <summary>Remaining fuel as a relative value (0..1).</summary>
        public double Fuel
        {
            get { return _unit != null ? _unit.GetDouble("fuel_relative") : 0; }
            set { if (_unit != null) _unit.SetFloat("fuel_relative", Clamp01(value)); }
        }

        /// <summary>Odometer reading in kilometres.</summary>
        public long Odometer
        {
            get { return _unit != null ? _unit.GetLong("odometer") : 0; }
        }

        /// <summary>License plate of the vehicle.</summary>
        public string LicensePlate
        {
            get { return _unit != null ? _unit.GetString("license_plate") : string.Empty; }
            set { if (_unit != null) _unit.SetScalarPreserveStyle("license_plate", value); }
        }

        private double GetWear(string name)
        {
            return _unit != null ? _unit.GetDouble(name) : 0;
        }

        private void SetWear(string name, double value)
        {
            if (_unit == null) return;
            // 使用游戏自身的浮点写法：整数值写成整数，小数值写成 &xxxxxxxx。
            _unit.SetFloat(name, Clamp01(value));
        }

        private List<SiiValue> GetWearArray(string name)
        {
            var result = new List<SiiValue>();
            if (_unit == null) return result;
            result.AddRange(_unit.GetArrayItems(name));
            return result;
        }

        private void SetWearArray(string name, double value)
        {
            if (_unit == null) return;
            var items = GetWearArray(name);
            if (items.Count == 0) return; // 数组为空时不要凭空创建元素

            double v = Clamp01(value);
            var updated = new List<SiiValue>(items.Count);
            foreach (var item in items)
            {
                if (v == 0)
                {
                    updated.Add(new IntValue(0));
                }
                else
                {
                    updated.Add(new FloatValue(v, item.IsHexFloat()));
                }
            }
            _unit.SetArrayItems(name, updated);
        }

        private static double Clamp01(double value)
        {
            return value < 0 ? 0 : (value > 1 ? 1 : value);
        }

        /// <summary>Resets all repairable wear values to 0 (brand new).</summary>
        public void Repair()
        {
            EngineWear = 0;
            ChassisWear = 0;
            TransmissionWear = 0;
            CabinWear = 0;
            WheelWear = 0;
        }

        /// <summary>Fills the tank up (the game stores a relative fuel level).</summary>
        public void Refuel()
        {
            Fuel = 1;
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
