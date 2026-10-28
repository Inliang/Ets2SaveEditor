using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ets2SaveEditor.Core.Sii.Model
{
    /// <summary>Kind of an SII value.</summary>
    public enum SiiValueKind
    {
        Int,
        Float,
        String,
        Bool,
        Vector,
        Reference,
        Array
    }

    /// <summary>
    /// Abstract base class for every value that can appear in an SII attribute.
    /// </summary>
    public abstract class SiiValue
    {
        /// <summary>Gets the kind of this value.</summary>
        public abstract SiiValueKind Kind { get; }

        /// <summary>Serializes this value back to its SII text representation.</summary>
        public abstract string ToSiiString();
    }

    /// <summary>64-bit integer value.</summary>
    public class IntValue : SiiValue
    {
        public long Value { get; set; }

        public IntValue(long value) { Value = value; }

        public override SiiValueKind Kind => SiiValueKind.Int;

        public override string ToSiiString() => Value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Floating point value.</summary>
    public class FloatValue : SiiValue
    {
        public double Value { get; set; }

        /// <summary>
        /// Whether the value must be written using the game's IEEE-754 bit
        /// pattern notation (<c>&amp;xxxxxxxx</c>). The game never writes
        /// decimal fractions, so hex notation is the canonical form.
        /// </summary>
        public bool IsHex { get; set; }

        public FloatValue(double value) { Value = value; }

        public FloatValue(double value, bool isHex) : this(value) { IsHex = isHex; }

        public override SiiValueKind Kind => SiiValueKind.Float;

        public override string ToSiiString()
        {
            if (IsHex)
            {
                float single = (float)Value;
                uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(single), 0);
                return "&" + bits.ToString("x8", CultureInfo.InvariantCulture);
            }

            double v = Value;
            if (!double.IsNaN(v) && !double.IsInfinity(v) &&
                v == Math.Floor(v) && Math.Abs(v) < 1e7)
            {
                // 与游戏一致：整数值直接写成整数。
                return ((long)v).ToString(CultureInfo.InvariantCulture);
            }

            return v.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Quoted string value.</summary>
    public class StringValue : SiiValue
    {
        public string Value { get; set; }

        public StringValue(string value) { Value = value ?? string.Empty; }

        public override SiiValueKind Kind => SiiValueKind.String;

        public override string ToSiiString()
        {
            string escaped = Value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "\"" + escaped + "\"";
        }
    }

    /// <summary>Boolean value.</summary>
    public class BoolValue : SiiValue
    {
        public bool Value { get; set; }

        public BoolValue(bool value) { Value = value; }

        public override SiiValueKind Kind => SiiValueKind.Bool;

        public override string ToSiiString() => Value ? "true" : "false";
    }

    /// <summary>
    /// Vector value: a parenthesised group of numeric components.
    /// <para>
    /// The game writes vectors in three flavours:
    /// <list type="bullet">
    ///   <item><description><c>(x, y)</c> — 2 components</description></item>
    ///   <item><description><c>(x, y, z)</c> — 3 components</description></item>
    ///   <item><description><c>(w; x, y, z)</c> — 4 components (quaternion style)</description></item>
    /// </list>
    /// A placement additionally carries a second (rotation) group:
    /// <c>(x, y, z) (w; a, b, c)</c>.
    /// </para>
    /// </summary>
    public class VectorValue : SiiValue
    {
        /// <summary>The components of this vector.</summary>
        public List<SiiValue> Components { get; set; }

        /// <summary>
        /// Separator used between the first and the second component.
        /// <c>", "</c> for ordinary vectors, <c>"; "</c> for quaternion style ones.
        /// </summary>
        public string FirstSeparator { get; set; }

        /// <summary>Optional second group (the rotation part of a placement).</summary>
        public VectorValue Rotation { get; set; }

        /// <summary>
        /// Verbatim text of the value. When set, it is emitted unchanged so
        /// that shapes the editor does not understand survive a round-trip.
        /// </summary>
        public string RawText { get; set; }

        public VectorValue() : this(new List<SiiValue>()) { }

        public VectorValue(List<SiiValue> components)
        {
            Components = components ?? new List<SiiValue>();
            FirstSeparator = ", ";
        }

        public VectorValue(double x, double y, double z)
            : this(new List<SiiValue> { new IntValue((long)x), new IntValue((long)y), new IntValue((long)z) })
        {
        }

        public override SiiValueKind Kind => SiiValueKind.Vector;

        /// <summary>First component as a number (0 when absent).</summary>
        public double X => GetNumber(0);

        /// <summary>Second component as a number (0 when absent).</summary>
        public double Y => GetNumber(1);

        /// <summary>Third component as a number (0 when absent).</summary>
        public double Z => GetNumber(2);

        public override string ToSiiString()
        {
            if (!string.IsNullOrEmpty(RawText))
            {
                return RawText;
            }

            var sb = new StringBuilder();
            sb.Append('(');
            for (int i = 0; i < Components.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(i == 1 ? FirstSeparator : ", ");
                }
                sb.Append(Components[i] != null ? Components[i].ToSiiString() : string.Empty);
            }
            sb.Append(')');

            if (Rotation != null)
            {
                sb.Append(' ').Append(Rotation.ToSiiString());
            }

            return sb.ToString();
        }

        private double GetNumber(int index)
        {
            if (index < 0 || index >= Components.Count) return 0;
            switch (Components[index])
            {
                case IntValue iv: return iv.Value;
                case FloatValue fv: return fv.Value;
                default: return 0;
            }
        }
    }

    /// <summary>Reference to another unit (nameless id, no quotes).</summary>
    public class ReferenceValue : SiiValue
    {
        public string TargetId { get; set; }

        public ReferenceValue(string targetId) { TargetId = targetId ?? string.Empty; }

        public override SiiValueKind Kind => SiiValueKind.Reference;

        public override string ToSiiString() => TargetId;
    }

    /// <summary>Array of values (syntactic sugar for repeated attributes).</summary>
    public class ArrayValue : SiiValue
    {
        public List<SiiValue> Items { get; set; }

        public ArrayValue(List<SiiValue> items) { Items = items ?? new List<SiiValue>(); }

        public override SiiValueKind Kind => SiiValueKind.Array;

        /// <summary>Array serialization is handled by the serializer; not emitted directly.</summary>
        public override string ToSiiString() => string.Empty;
    }
}
