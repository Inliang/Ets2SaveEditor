using System.Globalization;

namespace Ets2SaveEditor.Core.Sii.Model
{
    /// <summary>
    /// Convenience helpers for reading values out of an SII document.
    /// </summary>
    public static class SiiValueExtensions
    {
        /// <summary>
        /// Returns the textual identity of a value: references and bare strings
        /// keep their text, everything else is rendered with its SII notation.
        /// </summary>
        public static string AsId(this SiiValue value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case ReferenceValue rv:
                    return rv.TargetId;
                case StringValue sv:
                    return sv.Value;
                default:
                    return value.ToSiiString();
            }
        }

        /// <summary>Returns the numeric value of a value (0 when not numeric).</summary>
        public static double AsDouble(this SiiValue value)
        {
            switch (value)
            {
                case IntValue iv:
                    return iv.Value;
                case FloatValue fv:
                    return fv.Value;
                default:
                    return 0;
            }
        }

        /// <summary>Returns the numeric value of a value (0 when not numeric).</summary>
        public static long AsLong(this SiiValue value)
        {
            return (long)value.AsDouble();
        }

        /// <summary>
        /// Returns whether a value uses the game's hexadecimal float notation,
        /// so that a replacement value can keep the same notation.
        /// </summary>
        public static bool IsHexFloat(this SiiValue value)
        {
            return value is FloatValue fv && fv.IsHex;
        }
    }
}
