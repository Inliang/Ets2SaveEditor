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

        public FloatValue(double value) { Value = value; }

        public override SiiValueKind Kind => SiiValueKind.Float;

        public override string ToSiiString()
        {
            string s = Value.ToString(CultureInfo.InvariantCulture);
            return s.IndexOf('.') >= 0 ? s : s + ".0";
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

    /// <summary>3D vector value (x, y, z).</summary>
    public class VectorValue : SiiValue
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public VectorValue(double x, double y, double z) { X = x; Y = y; Z = z; }

        public override SiiValueKind Kind => SiiValueKind.Vector;

        public override string ToSiiString()
        {
            return "(" + Fmt(X) + ", " + Fmt(Y) + ", " + Fmt(Z) + ")";
        }

        private static string Fmt(double v)
        {
            string s = v.ToString(CultureInfo.InvariantCulture);
            return s.IndexOf('.') >= 0 ? s : s + ".0";
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
