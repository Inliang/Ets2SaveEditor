using System;
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

        /// <summary>
        /// Returns the id of a reference value, or an empty string when the
        /// value is not a usable reference.
        /// <para>
        /// 存档里的空引用存的是**字面量 <c>null</c>**（不是空串），而数组头
        /// （<c>name: 3</c>）又是一个整数。两者都必须当成「没有引用」，
        /// 否则会把 351 个待聘司机模板之类的占位符当成真实数据。
        /// </para>
        /// </summary>
        public static string AsReferenceId(this SiiValue value)
        {
            if (value == null) return string.Empty;

            // 只有引用（和引号里的字符串）才是 id；整数是数组的元素个数。
            if (!(value is ReferenceValue) && !(value is StringValue)) return string.Empty;

            return NormalizeReferenceId(value.AsId());
        }

        /// <summary>
        /// Normalizes a reference id: the game's empty reference is the literal
        /// <c>null</c>, which must be treated as "no reference at all".
        /// </summary>
        /// <param name="id">Raw reference id (may be null or empty).</param>
        /// <returns>The id, or an empty string when there is no reference.</returns>
        public static string NormalizeReferenceId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            if (string.Equals(id, "null", StringComparison.OrdinalIgnoreCase)) return string.Empty;
            return id;
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
