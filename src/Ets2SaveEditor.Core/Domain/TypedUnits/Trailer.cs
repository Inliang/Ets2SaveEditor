using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>trailer</c> unit.
    /// <para>
    /// A trailer stores its definition in <c>trailer_definition</c>, the wear
    /// of the body in <c>trailer_body_wear</c>, the wear of the chassis in
    /// <c>chassis_wear</c> and the wheel wear in the <c>wheels_wear</c> array.
    /// </para>
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

        /// <summary>Definition of the trailer (e.g. <c>trailer_def.scs.box.single_3.dryvan</c>).</summary>
        public string Definition
        {
            get { return _unit != null ? _unit.GetReference("trailer_definition") : string.Empty; }
        }

        /// <summary>Wear of the trailer body (0..1).</summary>
        public double BodyWear
        {
            get { return _unit != null ? _unit.GetDouble("trailer_body_wear") : 0; }
            set { if (_unit != null) SetWearValue(_unit, "trailer_body_wear", value); }
        }

        /// <summary>Wear of the chassis (0..1).</summary>
        public double ChassisWear
        {
            get { return _unit != null ? _unit.GetDouble("chassis_wear") : 0; }
            set { if (_unit != null) SetWearValue(_unit, "chassis_wear", value); }
        }

        /// <summary>
        /// Average wheel wear (0..1). The game stores one value per wheel in
        /// the <c>wheels_wear</c> array.
        /// </summary>
        public double WheelWear
        {
            get
            {
                if (_unit == null) return 0;
                var items = _unit.GetArrayItems("wheels_wear");
                if (items.Count == 0) return 0;
                double sum = 0;
                foreach (var item in items) sum += item.AsDouble();
                return sum / items.Count;
            }
            set { if (_unit != null) SetWearArray("wheels_wear", value); }
        }

        /// <summary>Damage of the transported cargo (0..1).</summary>
        public double CargoDamage
        {
            get { return _unit != null ? _unit.GetDouble("cargo_damage") : 0; }
            set { if (_unit != null) SetWearValue(_unit, "cargo_damage", value); }
        }

        /// <summary>Mass of the transported cargo in kilograms.</summary>
        public double CargoMass
        {
            get { return _unit != null ? _unit.GetDouble("cargo_mass") : 0; }
        }

        /// <summary>Whether the trailer is oversized.</summary>
        public bool Oversize
        {
            get { return _unit != null && _unit.GetBool("oversize"); }
        }

        private static double Clamp01(double value)
        {
            return value < 0 ? 0 : (value > 1 ? 1 : value);
        }

        private static void SetWearValue(SiiUnit unit, string name, double value)
        {
            // 使用游戏自身的浮点写法：整数值写成整数，小数值写成 &xxxxxxxx。
            unit.SetFloat(name, Clamp01(value));
        }

        private void SetWearArray(string name, double value)
        {
            if (_unit == null) return;
            var items = _unit.GetArrayItems(name);
            if (items.Count == 0) return;

            double v = Clamp01(value);
            var updated = new List<SiiValue>(items.Count);
            foreach (var item in items)
            {
                updated.Add(v == 0 ? (SiiValue)new IntValue(0) : new FloatValue(v, item.IsHexFloat()));
            }
            _unit.SetArrayItems(name, updated);
        }

        /// <summary>Resets all repairable wear values to 0.</summary>
        public void Repair()
        {
            BodyWear = 0;
            ChassisWear = 0;
            WheelWear = 0;
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
