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
        private readonly SiiDocument _document;
        private string _garageId;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Trailer(SiiUnit unit) : this(unit, null) { }

        /// <summary>
        /// Creates a wrapper that can also resolve referenced units
        /// (the <c>trailer_def</c> that carries the model name).
        /// </summary>
        public Trailer(SiiUnit unit, SiiDocument document)
        {
            _unit = unit;
            _document = document;
        }

        /// <summary>
        /// Identifier of the garage this trailer is parked in, or an empty
        /// string when the trailer is not in any garage.
        /// <para>
        /// A save file keeps a <c>trailer</c> unit for every trailer the player
        /// ever owned — including the ones that were sold or scrapped. Only the
        /// ones referenced by a <c>garage</c> unit are actually owned now.
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
                    foreach (var item in garage.GetArrayItems("trailers"))
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
        /// Whether the trailer is currently parked in a garage (i.e. owned).
        /// </summary>
        public bool IsInGarage => !string.IsNullOrEmpty(GarageId);

        /// <summary>Definition of the trailer (e.g. <c>trailer_def.scs.box.single_3.dryvan</c>).</summary>
        public string Definition
        {
            get { return _unit != null ? _unit.GetReference("trailer_definition") : string.Empty; }
        }

        /// <summary>
        /// Readable model name of the trailer, e.g. <c>Gooseneck Container</c>.
        /// <para>
        /// The save file points at a <c>trailer_def</c> unit whose
        /// <c>source_name</c> field carries the game's own resource path
        /// (<c>trailer_def.scs.gooseneck.singl_3e_40.container</c>). The
        /// meaningful part of that path is turned into a readable name.
        /// </para>
        /// </summary>
        public string ModelName
        {
            get
            {
                string source = SourceName;
                if (string.IsNullOrEmpty(source)) return string.Empty;

                // trailer_def.scs.<shape>.<variant>.<body> -> "Gooseneck Container"
                string[] parts = source.Split('.');
                if (parts.Length < 3) return source;

                string body = parts[parts.Length - 1];
                string shape = parts.Length >= 4 ? parts[parts.Length - 3] : string.Empty;
                string variant = parts.Length >= 5 ? parts[parts.Length - 4] : string.Empty;

                // 去掉厂商前缀 "scs"（游戏自制的挂车都带这个前缀，读起来多余）。
                string title = Titleize(shape);
                if (title == "scs") title = string.Empty;

                string prefix = string.Empty;
                if (!string.IsNullOrEmpty(variant) && variant != "scs") prefix = Titleize(variant);

                return string.Join(" ", new[] { prefix, title, Titleize(body) })
                    .Trim();
            }
        }

        /// <summary>
        /// The <c>source_name</c> of the referenced <c>trailer_def</c> unit,
        /// e.g. <c>trailer_def.scs.gooseneck.singl_3e_40.container</c>.
        /// </summary>
        public string SourceName
        {
            get
            {
                if (_unit == null) return string.Empty;
                string id = _unit.GetReference("trailer_definition");
                if (string.IsNullOrEmpty(id)) return string.Empty;

                // 存档里没有 trailer_def 单元时（如只有引用的场景），退回标识本身。
                var unit = _document != null ? _document.GetUnitById(id) : null;
                if (unit == null) return string.Empty;
                return unit.GetString("source_name");
            }
        }

        private static string Titleize(string token)
        {
            if (string.IsNullOrEmpty(token)) return string.Empty;

            var parts = token.Split('_');
            var builder = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(char.ToUpperInvariant(part[0]));
                builder.Append(part.Substring(1));
            }
            return builder.ToString();
        }

        /// <summary>
        /// License plate of the trailer (e.g. <c>DW 7949A</c>).
        /// <para>
        /// The stored value may carry a country suffix
        /// (<c>"DW 7949A|poland"</c>); only the plate part is returned.
        /// </para>
        /// </summary>
        public string LicensePlate
        {
            get
            {
                if (_unit == null) return string.Empty;
                string plate = _unit.GetString("license_plate");
                if (string.IsNullOrEmpty(plate)) return string.Empty;

                // 未上牌的挂车用带 HTML 标签的占位文本，逐层剥掉标签。
                int open = plate.IndexOf('<');
                while (open >= 0)
                {
                    int close = plate.IndexOf('>', open);
                    if (close < 0) break;
                    plate = plate.Substring(0, open) + plate.Substring(close + 1);
                    open = plate.IndexOf('<');
                }

                int pipe = plate.IndexOf('|');
                return pipe >= 0 ? plate.Substring(0, pipe) : plate;
            }
            set
            {
                if (_unit == null) return;
                string plate = value ?? string.Empty;
                int pipe = plate.IndexOf('|');
                if (pipe >= 0) plate = plate.Substring(0, pipe);

                // 写回时保留原存档里的国家后缀（形如 "DW 7949A|poland"）。
                string existing = _unit.GetString("license_plate");
                int existingPipe = existing != null ? existing.IndexOf('|') : -1;
                if (existingPipe >= 0) plate += existing.Substring(existingPipe);

                _unit.SetScalarPreserveStyle("license_plate", plate);
            }
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
                list.Add(new Trailer(u, doc));
            }
            return list;
        }
    }
}
