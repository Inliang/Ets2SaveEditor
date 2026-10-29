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
        private readonly SiiDocument _document;
        private string _garageId;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Vehicle(SiiUnit unit) : this(unit, null) { }

        /// <summary>
        /// Creates a wrapper that can also resolve the garage the vehicle
        /// is parked in.
        /// </summary>
        public Vehicle(SiiUnit unit, SiiDocument document)
        {
            _unit = unit;
            _document = document;
        }

        /// <summary>
        /// Identifier of the garage this vehicle is parked in, or an empty
        /// string when the vehicle is not in any garage.
        /// <para>
        /// A save file keeps a <c>vehicle</c> unit for every truck the player
        /// ever owned — including the ones that were sold. Only the ones
        /// referenced by a <c>garage</c> unit are actually owned right now.
        /// </para>
        /// </summary>
        public string GarageId
        {
            get
            {
                if (_garageId != null) return _garageId;

                _garageId = string.Empty;
                if (_document == null || _unit == null) return _garageId;

                string id = _unit.Id;
                foreach (var garage in _document.GetUnitsByType(Constants.GarageType))
                {
                    foreach (var item in garage.GetArrayItems("vehicles"))
                    {
                        if (string.Equals(item.AsId(), id, System.StringComparison.Ordinal))
                        {
                            _garageId = garage.Id;
                            return _garageId;
                        }
                    }
                }
                return _garageId;
            }
        }

        /// <summary>
        /// Whether the vehicle is currently parked in a garage (i.e. owned).
        /// </summary>
        public bool IsInGarage => !string.IsNullOrEmpty(GarageId);

        /// <summary>
        /// City the garage of this vehicle is in (readable name; empty when
        /// the vehicle is not parked in a garage).
        /// </summary>
        public string GarageCity
        {
            get
            {
                string garage = GarageId;
                if (string.IsNullOrEmpty(garage)) return string.Empty;
                int dot = garage.LastIndexOf('.');
                string token = dot >= 0 && dot < garage.Length - 1
                    ? garage.Substring(dot + 1)
                    : garage;
                return GameData.CityCatalogue.DisplayName(token);
            }
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
        /// <summary>
        /// License plate of the truck (e.g. <c>DW 853SA</c>).
        /// <para>
        /// The stored value may carry a country suffix
        /// (<c>"DW 853SA|poland"</c>) and, for trailers that were never
        /// registered, an HTML-formatted placeholder. Only the plain plate
        /// part is returned so the table stays readable.
        /// </para>
        /// </summary>
        public string LicensePlate
        {
            get
            {
                if (_unit == null) return string.Empty;
                string plate = _unit.GetString("license_plate");
                if (string.IsNullOrEmpty(plate)) return string.Empty;

                // 未上牌的车用的是带 HTML 标签的占位文本
                // （形如 "<align right=148><font ...>AF 3537</font></align>"），
                // 逐层剥掉标签只留最内层的文字。
                int open = plate.IndexOf('<');
                while (open >= 0)
                {
                    int close = plate.IndexOf('>', open);
                    if (close < 0) break;
                    plate = plate.Substring(0, open) + plate.Substring(close + 1);
                    open = plate.IndexOf('<');
                }
                int pipe = plate.IndexOf('|');
                if (pipe >= 0) plate = plate.Substring(0, pipe);
                return plate.Trim();
            }
            set
            {
                if (_unit == null) return;
                string plate = value ?? string.Empty;
                int pipe = plate.IndexOf('|');
                if (pipe >= 0) plate = plate.Substring(0, pipe);

                // 写回时保留原存档里的国家后缀（形如 "DW 853SA|poland"），
                // 否则游戏读档时会认不出车牌所属国家。
                string existing = _unit.GetString("license_plate");
                int existingPipe = existing != null ? existing.IndexOf('|') : -1;
                if (existingPipe >= 0) plate += existing.Substring(existingPipe);

                _unit.SetScalarPreserveStyle("license_plate", plate);
            }
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
                list.Add(new Vehicle(u, doc));
            }
            return list;
        }
    }
}
