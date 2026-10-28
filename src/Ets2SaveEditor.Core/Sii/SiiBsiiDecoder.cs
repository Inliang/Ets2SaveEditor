using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ets2SaveEditor.Core.Common;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>
    /// Decoder for the binary <c>BSII</c> payload used by modern ETS2/ATS saves.
    /// <para>
    /// Container layout:
    /// <code>
    /// [0..4)    "BSII"
    /// [4..8)    u32 version (1 / 2 / 3)
    /// blocks... "BSII" blockType == 0 → 结构定义块
    ///          "BSII" blockType != 0 → 数据块（blockType 即 structureId）
    /// </code>
    /// The produced text is byte-for-byte compatible with the plaintext SII that
    /// the game itself writes (see <c>game_backup.sii</c>):
    /// <list type="bullet">
    ///   <item><description>unit header: <c>name : id {</c></description></item>
    ///   <item><description>attribute: <c>name: value</c>（冒号前无空格）</description></item>
    ///   <item><description>array: <c>name: count</c> followed by <c>name[i]: value</c></description></item>
    /// </list>
    /// </summary>
    public static class SiiBsiiDecoder
    {
        /// <summary>Magic marker at the beginning of every binary payload.</summary>
        public const string Magic = "BSII";

        /// <summary>Base-38 alphabet used by encoded strings (digit 0 is unused).</summary>
        private const string EncodedAlphabet = "0123456789abcdefghijklmnopqrstuvwxyz_";

        /// <summary>Line separator used by the game.</summary>
        private const string NewLine = "\r\n";

        // ---- BSII value type identifiers -------------------------------------
        private const uint TypeString = 0x01;
        private const uint TypeStringArray = 0x02;
        private const uint TypeEncoded = 0x03;
        private const uint TypeEncodedArray = 0x04;
        private const uint TypeFloat = 0x05;
        private const uint TypeFloatArray = 0x06;
        private const uint TypeVec2f = 0x07;
        private const uint TypeVec2fArray = 0x08;
        private const uint TypeVec3f = 0x09;
        private const uint TypeVec3fArray = 0x0a;
        private const uint TypeVec3i = 0x11;
        private const uint TypeVec3iArray = 0x12;
        private const uint TypeVec4f = 0x17;
        private const uint TypeVec4fArray = 0x18;
        private const uint TypePlacement = 0x19;
        private const uint TypePlacementArray = 0x1a;
        private const uint TypeInt32 = 0x25;
        private const uint TypeInt32Array = 0x26;
        private const uint TypeUInt32 = 0x27;
        private const uint TypeUInt32Array = 0x28;
        private const uint TypeInt16 = 0x29;
        private const uint TypeInt16Array = 0x2a;
        private const uint TypeUInt16 = 0x2b;
        private const uint TypeUInt16Array = 0x2c;
        private const uint TypeUInt32Alt = 0x2f;
        private const uint TypeInt64 = 0x31;
        private const uint TypeInt64Array = 0x32;
        private const uint TypeUInt64 = 0x33;
        private const uint TypeUInt64Array = 0x34;
        private const uint TypeBool = 0x35;
        private const uint TypeBoolArray = 0x36;
        private const uint TypeOrdinal = 0x37;
        private const uint TypeId = 0x39;
        private const uint TypeIdArray = 0x3a;
        private const uint TypeIdAlt = 0x3b;
        private const uint TypeIdArrayAlt = 0x3c;
        private const uint TypeIdAlt2 = 0x3d;
        private const uint TypeIdArrayE = 0x3e;

        /// <summary>
        /// Textual representation of an unset optional value. The game stores
        /// "no value" as the maximum value of the respective unsigned type and
        /// writes it as <c>nil</c>.
        /// </summary>
        private const string NilSentinel = "nil";

        /// <summary>
        /// Decodes a binary BSII payload into plaintext SII text.
        /// </summary>
        /// <param name="binary">Raw payload starting with the <c>BSII</c> magic.</param>
        /// <returns>The SII text (UTF-8 compatible, CRLF line endings).</returns>
        /// <exception cref="SiiFormatException">When the payload is malformed.</exception>
        public static string Decode(byte[] binary)
        {
            if (binary == null)
            {
                throw new ArgumentNullException(nameof(binary));
            }

            if (binary.Length < 8)
            {
                throw new SiiFormatException("BSII 数据过短，结构不完整。");
            }

            if (Encoding.ASCII.GetString(binary, 0, Magic.Length) != Magic)
            {
                throw new SiiFormatException("BSII 魔数不匹配，无法解码二进制 SII。");
            }

            var reader = new BsiiReader(binary);
            reader.Skip(Magic.Length); // 跳过 "BSII" 魔数
            uint version = reader.ReadUInt32();

            var structures = new Dictionary<uint, BsiiStructure>();
            var sb = new StringBuilder();
            sb.Append(Constants.SiiHeader).Append(NewLine);
            sb.Append('{').Append(NewLine);

            while (true)
            {
                uint blockType = reader.ReadUInt32();

                if (blockType == 0)
                {
                    // 结构定义块
                    byte validity = reader.ReadByte();
                    if (validity == 0)
                    {
                        // 结束标记
                        break;
                    }

                    uint structureId = reader.ReadUInt32();
                    if (structureId == 0)
                    {
                        throw new SiiFormatException("BSII 结构 ID 不能为 0。");
                    }

                    string structureName = reader.ReadString();
                    var fields = new List<BsiiField>();

                    while (true)
                    {
                        uint typeId = reader.ReadUInt32();
                        if (typeId == 0)
                        {
                            break; // 字段列表结束
                        }

                        string fieldName = reader.ReadString();
                        Dictionary<uint, string> ordinalValues = null;

                        if (typeId == TypeOrdinal)
                        {
                            // ORDINAL 字段在结构定义里携带 (key, value) 查找表。
                            uint entryCount = reader.ReadUInt32();
                            ordinalValues = new Dictionary<uint, string>();
                            for (uint i = 0; i < entryCount; i++)
                            {
                                uint key = reader.ReadUInt32();
                                string value = reader.ReadString();
                                ordinalValues[key] = value;
                            }
                        }

                        fields.Add(new BsiiField(typeId, fieldName, ordinalValues));
                    }

                    structures[structureId] = new BsiiStructure(structureName, fields);
                    continue;
                }

                // 数据块
                if (!structures.TryGetValue(blockType, out BsiiStructure structure))
                {
                    throw new SiiFormatException(
                        "BSII 数据块引用了未定义的结构：" + blockType.ToString(CultureInfo.InvariantCulture));
                }

                string unitId = reader.ReadId();

                sb.Append(structure.Name).Append(" : ").Append(unitId).Append(" {").Append(NewLine);
                foreach (BsiiField field in structure.Fields)
                {
                    WriteField(sb, reader, field, version);
                }
                sb.Append('}').Append(NewLine).Append(NewLine);
            }

            sb.Append('}').Append(NewLine);
            return sb.ToString();
        }

        /// <summary>Writes one field (scalar or array) of a data block.</summary>
        private static void WriteField(StringBuilder sb, BsiiReader reader, BsiiField field, uint version)
        {
            if (!IsArrayType(field.TypeId))
            {
                sb.Append(' ').Append(field.Name).Append(": ")
                  .Append(ReadValueText(reader, field.TypeId, field, version))
                  .Append(NewLine);
                return;
            }

            uint count = reader.ReadUInt32();
            sb.Append(' ').Append(field.Name).Append(": ")
              .Append(count.ToString(CultureInfo.InvariantCulture)).Append(NewLine);

            uint elementType = ElementType(field.TypeId);
            for (uint i = 0; i < count; i++)
            {
                sb.Append(' ').Append(field.Name).Append('[')
                  .Append(i.ToString(CultureInfo.InvariantCulture)).Append("]: ")
                  .Append(ReadValueText(reader, elementType, field, version))
                  .Append(NewLine);
            }
        }

        /// <summary>Reads a single value and returns its SII text representation.</summary>
        private static string ReadValueText(BsiiReader reader, uint typeId, BsiiField field, uint version)
        {
            switch (typeId)
            {
                case TypeString:
                    return FormatStringLiteral(reader.ReadString());

                case TypeEncoded:
                    // 编码字符串以裸文本写出，空串写成 ""（与游戏一致）。
                    return FormatStringLiteral(reader.ReadEncodedString());

                case TypeFloat:
                    return FormatFloat(reader.ReadSingle());

                case TypeVec2f:
                    return FormatFloatComponents(reader, 2, ", ");

                case TypeVec3f:
                    return FormatFloatComponents(reader, 3, ", ");

                case TypeVec3i:
                    return FormatIntComponents(reader, 3);

                case TypeVec4f:
                    // 四分量向量使用 "w; x, y, z" 的四元数风格写法。
                    return FormatFloatComponents(reader, 4, "; ");

                case TypePlacement:
                    return FormatPlacement(reader, version);

                case TypeInt32:
                    return reader.ReadInt32().ToString(CultureInfo.InvariantCulture);

                case TypeInt16:
                    return reader.ReadInt16().ToString(CultureInfo.InvariantCulture);

                case TypeUInt32:
                case TypeUInt32Alt:
                {
                    uint value = reader.ReadUInt32();
                    // 无符号类型的最大值即“无值”哨兵，游戏写作 nil。
                    return value == uint.MaxValue
                        ? NilSentinel
                        : value.ToString(CultureInfo.InvariantCulture);
                }

                case TypeUInt16:
                {
                    ushort value = reader.ReadUInt16();
                    return value == ushort.MaxValue
                        ? NilSentinel
                        : value.ToString(CultureInfo.InvariantCulture);
                }

                case TypeInt64:
                    return reader.ReadInt64().ToString(CultureInfo.InvariantCulture);

                case TypeUInt64:
                {
                    ulong value = reader.ReadUInt64();
                    return value == ulong.MaxValue
                        ? NilSentinel
                        : value.ToString(CultureInfo.InvariantCulture);
                }

                case TypeBool:
                    return reader.ReadByte() != 0 ? "true" : "false";

                case TypeOrdinal:
                {
                    uint key = reader.ReadUInt32();
                    if (field.OrdinalValues != null && field.OrdinalValues.TryGetValue(key, out string value))
                    {
                        return FormatStringLiteral(value);
                    }
                    return key.ToString(CultureInfo.InvariantCulture);
                }

                case TypeId:
                case TypeIdAlt:
                case TypeIdAlt2:
                    return reader.ReadId();

                default:
                    throw new SiiFormatException(
                        "不支持的 BSII 数据类型：0x" + typeId.ToString("x2", CultureInfo.InvariantCulture));
            }
        }

        private static string FormatFloatComponents(BsiiReader reader, int count, string firstSeparator)
        {
            var sb = new StringBuilder();
            sb.Append('(');
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(i == 1 ? firstSeparator : ", ");
                sb.Append(FormatFloat(reader.ReadSingle()));
            }
            sb.Append(')');
            return sb.ToString();
        }

        private static string FormatIntComponents(BsiiReader reader, int count)
        {
            var sb = new StringBuilder();
            sb.Append('(');
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(reader.ReadInt32().ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(')');
            return sb.ToString();
        }

        /// <summary>
        /// Reads a placement (position + rotation) and returns its SII text.
        /// Version 2/3 stores an extra float that carries the position bias.
        /// </summary>
        private static string FormatPlacement(BsiiReader reader, uint version)
        {
            if (version >= 2)
            {
                float v0 = reader.ReadSingle();
                float v1 = reader.ReadSingle();
                float v2 = reader.ReadSingle();
                float biasCarrier = reader.ReadSingle();
                float v4 = reader.ReadSingle();
                float v5 = reader.ReadSingle();
                float v6 = reader.ReadSingle();
                float v7 = reader.ReadSingle();

                // 偏移量被编码进第 4 个浮点数的整数值里（低 12 位为 X，高 12 位为 Z）。
                long coef = (long)biasCarrier;
                float x = (float)(v0 + (((coef & 0xfff) - 2048) << 9));
                float z = (float)(v2 + ((((coef >> 12) & 0xfff) - 2048) << 9));

                return "(" + FormatFloat(x) + ", " + FormatFloat(v1) + ", " + FormatFloat(z) + ") (" +
                       FormatFloat(v4) + "; " + FormatFloat(v5) + ", " + FormatFloat(v6) + ", " + FormatFloat(v7) + ")";
            }

            float p0 = reader.ReadSingle();
            float p1 = reader.ReadSingle();
            float p2 = reader.ReadSingle();
            float q0 = reader.ReadSingle();
            float q1 = reader.ReadSingle();
            float q2 = reader.ReadSingle();
            float q3 = reader.ReadSingle();

            return "(" + FormatFloat(p0) + ", " + FormatFloat(p1) + ", " + FormatFloat(p2) + ") (" +
                   FormatFloat(q0) + "; " + FormatFloat(q1) + ", " + FormatFloat(q2) + ", " + FormatFloat(q3) + ")";
        }

        /// <summary>
        /// Formats a float the way the game does: integral values are written as
        /// plain integers, everything else as an IEEE-754 bit pattern
        /// (<c>&amp;xxxxxxxx</c>).
        /// </summary>
        public static string FormatFloat(float value)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value) &&
                value == MathF.Floor(value) && MathF.Abs(value) < 1e7f)
            {
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            }

            uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            return "&" + bits.ToString("x8", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a string literal: bare when it only contains
        /// <c>[A-Za-z0-9_]</c>, quoted (and escaped) otherwise.
        /// </summary>
        public static string FormatStringLiteral(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            bool bare = true;
            foreach (char c in value)
            {
                bool allowed = (c >= 'a' && c <= 'z') ||
                               (c >= 'A' && c <= 'Z') ||
                               (c >= '0' && c <= '9') ||
                               c == '_';
                if (!allowed)
                {
                    bare = false;
                    break;
                }
            }

            if (bare)
            {
                return value;
            }

            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                if (c == '"' || c == '\\')
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>Whether the given type id denotes an array type.</summary>
        private static bool IsArrayType(uint typeId)
        {
            switch (typeId)
            {
                case TypeStringArray:
                case TypeEncodedArray:
                case TypeFloatArray:
                case TypeVec2fArray:
                case TypeVec3fArray:
                case TypeVec3iArray:
                case TypeVec4fArray:
                case TypePlacementArray:
                case TypeInt32Array:
                case TypeUInt32Array:
                case TypeInt16Array:
                case TypeUInt16Array:
                case TypeInt64Array:
                case TypeUInt64Array:
                case TypeBoolArray:
                case TypeIdArray:
                case TypeIdArrayAlt:
                case TypeIdArrayE:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Maps an array type id to its element type id.</summary>
        private static uint ElementType(uint arrayType)
        {
            switch (arrayType)
            {
                case TypeStringArray: return TypeString;
                case TypeEncodedArray: return TypeEncoded;
                case TypeFloatArray: return TypeFloat;
                case TypeVec2fArray: return TypeVec2f;
                case TypeVec3fArray: return TypeVec3f;
                case TypeVec3iArray: return TypeVec3i;
                case TypeVec4fArray: return TypeVec4f;
                case TypePlacementArray: return TypePlacement;
                case TypeInt32Array: return TypeInt32;
                case TypeUInt32Array: return TypeUInt32;
                case TypeInt16Array: return TypeInt16;
                case TypeUInt16Array: return TypeUInt16;
                case TypeInt64Array: return TypeInt64;
                case TypeUInt64Array: return TypeUInt64;
                case TypeBoolArray: return TypeBool;
                case TypeIdArray: return TypeId;
                case TypeIdArrayAlt: return TypeIdAlt;
                case TypeIdArrayE: return TypeId;
                default:
                    throw new SiiFormatException(
                        "未知的 BSII 数组类型：0x" + arrayType.ToString("x2", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>A structure definition block.</summary>
        private sealed class BsiiStructure
        {
            public string Name { get; }

            public List<BsiiField> Fields { get; }

            public BsiiStructure(string name, List<BsiiField> fields)
            {
                Name = name;
                Fields = fields;
            }
        }

        /// <summary>A single field of a structure definition block.</summary>
        private sealed class BsiiField
        {
            public uint TypeId { get; }

            public string Name { get; }

            /// <summary>Lookup table for ORDINAL fields (null for other types).</summary>
            public Dictionary<uint, string> OrdinalValues { get; }

            public BsiiField(uint typeId, string name, Dictionary<uint, string> ordinalValues)
            {
                TypeId = typeId;
                Name = name;
                OrdinalValues = ordinalValues;
            }
        }

        /// <summary>Little-endian reader over the BSII payload.</summary>
        private sealed class BsiiReader
        {
            private readonly byte[] _data;
            private int _position;

            public BsiiReader(byte[] data)
            {
                _data = data;
                _position = 0;
            }

            public byte ReadByte()
            {
                EnsureAvailable(1);
                return _data[_position++];
            }

            /// <summary>Skips the given number of bytes.</summary>
            public void Skip(int count)
            {
                EnsureAvailable(count);
                _position += count;
            }

            public ushort ReadUInt16()
            {
                EnsureAvailable(2);
                ushort value = (ushort)(_data[_position] | (_data[_position + 1] << 8));
                _position += 2;
                return value;
            }

            public short ReadInt16()
            {
                return unchecked((short)ReadUInt16());
            }

            public uint ReadUInt32()
            {
                EnsureAvailable(4);
                uint value = (uint)(
                    _data[_position] |
                    (_data[_position + 1] << 8) |
                    (_data[_position + 2] << 16) |
                    (_data[_position + 3] << 24));
                _position += 4;
                return value;
            }

            public int ReadInt32() => unchecked((int)ReadUInt32());

            public ulong ReadUInt64()
            {
                EnsureAvailable(8);
                ulong value = BitConverter.ToUInt64(_data, _position);
                _position += 8;
                return value;
            }

            public long ReadInt64() => unchecked((long)ReadUInt64());

            public float ReadSingle()
            {
                EnsureAvailable(4);
                float value = BitConverter.ToSingle(_data, _position);
                _position += 4;
                return value;
            }

            /// <summary>Reads a length-prefixed UTF-8 string.</summary>
            public string ReadString()
            {
                uint length = ReadUInt32();
                EnsureAvailable(checked((int)length));
                string value = Encoding.UTF8.GetString(_data, _position, (int)length);
                _position += (int)length;
                return value;
            }

            /// <summary>
            /// Reads an encoded string (base-38, little-endian digit order).
            /// </summary>
            public string ReadEncodedString()
            {
                ulong value = ReadUInt64();
                value &= ~(1UL << 63);

                if (value == 0)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();
                while (value > 0)
                {
                    int digit = (int)(value % 38);
                    if (digit == 0)
                    {
                        throw new SiiFormatException("非法的 BSII 编码字符串。");
                    }
                    sb.Append(EncodedAlphabet[digit - 1]);
                    value = (value - (ulong)digit) / 38;
                }
                return sb.ToString();
            }

            /// <summary>
            /// Reads an identifier: either a nameless id (16-bit groups) or a
            /// list of encoded strings joined by dots.
            /// </summary>
            public string ReadId()
            {
                byte partCount = ReadByte();

                if (partCount == 0xFF)
                {
                    return FormatNamelessId(ReadUInt64());
                }

                if (partCount == 0)
                {
                    return "null";
                }

                var parts = new string[partCount];
                for (int i = 0; i < partCount; i++)
                {
                    parts[i] = ReadEncodedString();
                }
                return string.Join(".", parts);
            }

            /// <summary>
            /// Formats a nameless id: the value is split into 16-bit groups
            /// (little-endian); the highest group is printed without padding,
            /// every lower group with four hex digits.
            /// Example: <c>_nameless.278.bdc9.2018</c>.
            /// </summary>
            private static string FormatNamelessId(ulong raw)
            {
                ulong value = raw & ~(1UL << 63);
                if (value == 0)
                {
                    return "_nameless.0";
                }

                var groups = new List<ushort>();
                while (value != 0)
                {
                    groups.Add((ushort)(value & 0xFFFF));
                    value >>= 16;
                }

                var sb = new StringBuilder("_nameless.");
                sb.Append(groups[groups.Count - 1].ToString("x", CultureInfo.InvariantCulture));
                for (int i = groups.Count - 2; i >= 0; i--)
                {
                    sb.Append('.').Append(groups[i].ToString("x4", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }

            private void EnsureAvailable(int count)
            {
                if (count < 0 || _position + count > _data.Length)
                {
                    throw new SiiFormatException("BSII 数据在读取过程中意外结束。");
                }
            }
        }
    }
}
