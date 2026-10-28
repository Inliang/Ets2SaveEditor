using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>
    /// Parses plaintext SII text into a lossless <see cref="SiiDocument"/>.
    /// Any structure that is not recognized is preserved verbatim in
    /// <see cref="SiiUnit.RawLines"/> so re-saving never corrupts a save file.
    /// </summary>
    public static class SiiParser
    {
        private static readonly Regex UnitStartRegex =
            new Regex(@"^\s*(?<type>[\w\.]+)\s*:\s*(?<id>.+?)\s*\{\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Matches a single attribute line, optionally carrying an array index
        /// (<c>name[3]: value</c>).
        /// </summary>
        private static readonly Regex AttributeRegex =
            new Regex(@"^\s*(?<name>[\w\.]+)(?:\[(?<idx>\d+)\])?\s*:\s*(?<val>.*)$", RegexOptions.Compiled);

        private static readonly Regex IntegerRegex = new Regex(@"^-?\d+$", RegexOptions.Compiled);

        /// <summary>
        /// Parses the given SII text into a <see cref="SiiDocument"/>.
        /// </summary>
        /// <exception cref="SiiFormatException">When the text is empty or malformed.</exception>
        public static SiiDocument Parse(string text)
        {
            return Parse(text, SiiSchema.Empty);
        }

        /// <summary>
        /// Parses the given SII text into a <see cref="SiiDocument"/> using the
        /// given schema.
        /// <para>
        /// The schema tells the parser which attributes are arrays. Without it
        /// an empty array (<c>name: 0</c> with no indexed lines) cannot be
        /// distinguished from an ordinary integer attribute, which would make
        /// e.g. the wheel wear of a trailer look like a single value and make
        /// the tool write an array with one element back — corrupting the
        /// save file.
        /// </para>
        /// </summary>
        /// <exception cref="SiiFormatException">When the text is empty or malformed.</exception>
        public static SiiDocument Parse(string text, SiiSchema schema)
        {
            var doc = new SiiDocument();
            doc.Schema = schema ?? SiiSchema.Empty;

            if (string.IsNullOrEmpty(text))
            {
                throw new SiiFormatException("Cannot parse an empty SII document.");
            }

            // Normalize line endings and split into lines (keep original for RawLines).
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');

            // Find the header line ("SiiNunit").
            int headerIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == Constants.SiiHeader)
                {
                    headerIndex = i;
                    doc.Header = Constants.SiiHeader;
                    break;
                }
            }

            // Find the first top-level opening brace (document block start).
            int bodyStart = -1;
            int searchFrom = headerIndex >= 0 ? headerIndex : 0;
            for (int i = searchFrom; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "{")
                {
                    bodyStart = i;
                    break;
                }
            }

            if (bodyStart < 0)
            {
                throw new SiiFormatException("Missing top-level block in SII document.");
            }

            int index = bodyStart + 1;
            int topLevelBrace = 1; // we just entered the document block.

            while (index < lines.Length)
            {
                string trimmed = lines[index].Trim();

                if (trimmed == "}")
                {
                    topLevelBrace--;
                    index++;
                    if (topLevelBrace == 0) break; // document block closed
                    continue;
                }

                if (trimmed == "{")
                {
                    topLevelBrace++;
                    index++;
                    continue;
                }

                if (UnitStartRegex.IsMatch(trimmed))
                {
                    index = ParseUnit(lines, index, doc);
                    continue;
                }

                // Unknown top-level content; skip (does not normally occur).
                index++;
            }

            return doc;
        }

        private static int ParseUnit(string[] lines, int unitLineIndex, SiiDocument doc)
        {
            string trimmed = lines[unitLineIndex].Trim();
            Match m = UnitStartRegex.Match(trimmed);
            string type = m.Groups["type"].Value;
            string id = m.Groups["id"].Value.Trim();

            // Strip surrounding quotes from the id if present.
            if (id.Length >= 2 && id[0] == '"' && id[id.Length - 1] == '"')
            {
                id = id.Substring(1, id.Length - 2);
            }

            var unit = new SiiUnit(type, id);
            doc.Units.Add(unit);

            int index = unitLineIndex + 1;
            int brace = 1; // unit opening brace

            while (index < lines.Length)
            {
                string line = lines[index];
                string t = line.Trim();

                if (t == "}")
                {
                    brace--;
                    index++;
                    if (brace == 0) return index; // unit body closed
                    continue;
                }

                // Nested block (bare "{" or "name : id {").
                if (t == "{" || UnitStartRegex.IsMatch(t))
                {
                    index = CollectRawBlock(lines, index, unit);
                    continue;
                }

                Match attrMatch = AttributeRegex.Match(t);
                if (attrMatch.Success)
                {
                    string name = attrMatch.Groups["name"].Value;
                    string val = attrMatch.Groups["val"].Value.TrimEnd();
                    Group indexGroup = attrMatch.Groups["idx"];

                    if (val.Length > 0 && !val.StartsWith("{"))
                    {
                        SiiValue value = ToSiiValue(val);
                        if (indexGroup.Success)
                        {
                            int arrayIndex;
                            if (int.TryParse(indexGroup.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out arrayIndex))
                            {
                                AddArrayElement(unit, name, value, arrayIndex);
                            }
                            else
                            {
                                AddAttribute(unit, name, value, line);
                            }
                        }
                        else if (doc.Schema.IsArrayField(type, name) && value is IntValue)
                        {
                            // 数组头（元素个数）。个数本身不是数据：真正的元素
                            // 由随后的 name[i] 行给出；空数组则没有任何元素行。
                            AddArrayHeader(unit, name, value, line);
                        }
                        else
                        {
                            AddAttribute(unit, name, value, line);
                        }
                        index++;
                        continue;
                    }                }

                // Anything else (comment, blank, unrecognized) -> raw.
                unit.RawLines.Add(line);
                index++;
            }

            // Reached EOF without a closing brace; preserve what we have.
            return index;
        }

        private static int CollectRawBlock(string[] lines, int start, SiiUnit unit)
        {
            int brace = 0;
            int i = start;
            while (i < lines.Length)
            {
                string t = lines[i].Trim();
                if (t == "{") brace++;
                else if (t == "}") brace--;

                unit.RawLines.Add(lines[i]);
                i++;

                if (brace == 0) break;
            }
            return i;
        }

        private static void AddAttribute(SiiUnit unit, string name, SiiValue value, string rawText)
        {
            var existing = unit.GetAttribute(name);
            if (existing == null)
            {
                unit.Attributes.Add(new SiiAttribute(name, value) { RawText = rawText });
                return;
            }

            if (existing.IsArray)
            {
                if (existing.Value is ArrayValue arr)
                {
                    arr.Items.Add(value);
                }
                return;
            }

            // Convert the single value into an array containing old + new.
            var newArray = new ArrayValue(new List<SiiValue> { existing.Value, value });
            existing.Value = newArray;
            existing.IsArray = true;
        }

        /// <summary>
        /// Records the header line of an array (the element count).
        /// <para>
        /// The count itself is not data — the elements follow as
        /// <c>name[i]</c> lines and the serializer regenerates the count. The
        /// attribute is therefore created as an empty array right away, so an
        /// array without any element (count 0) stays an empty array instead of
        /// being mistaken for an integer attribute.
        /// </para>
        /// </summary>
        private static void AddArrayHeader(SiiUnit unit, string name, SiiValue value, string rawText)
        {
            var existing = unit.GetAttribute(name);
            if (existing == null)
            {
                unit.Attributes.Add(new SiiAttribute(name, new ArrayValue(new List<SiiValue>()))
                {
                    IsArray = true,
                    RawText = rawText
                });
                return;
            }

            if (existing.Value is ArrayValue arr)
            {
                existing.Value = arr;
                existing.IsArray = true;
                return;
            }

            // 兜底：已有标量值，转成数组。
            existing.Value = new ArrayValue(new List<SiiValue>());
            existing.IsArray = true;
        }

        /// <summary>
        /// Adds an indexed array element. The preceding <c>name: count</c> line
        /// is replaced by the array itself; the count is recomputed on output.
        /// </summary>
        private static void AddArrayElement(SiiUnit unit, string name, SiiValue value, int arrayIndex)
        {
            var existing = unit.GetAttribute(name);

            if (existing == null)
            {
                var arr = new ArrayValue(new List<SiiValue> { value });
                unit.Attributes.Add(new SiiAttribute(name, arr) { IsArray = true });
                return;
            }

            if (existing.IsArray && existing.Value is ArrayValue array)
            {
                while (array.Items.Count < arrayIndex)
                {
                    array.Items.Add(new StringValue(string.Empty));
                }
                if (arrayIndex < array.Items.Count)
                {
                    array.Items[arrayIndex] = value;
                }
                else
                {
                    array.Items.Add(value);
                }
                return;
            }

            // The current value is only the element count: replace it with the array.
            var newArray = new ArrayValue(new List<SiiValue> { value });
            existing.Value = newArray;
            existing.IsArray = true;
        }

        /// <summary>
        /// Interprets a raw attribute value string as an <see cref="SiiValue"/>.
        /// </summary>
        public static SiiValue ToSiiValue(string v)
        {
            string s = v.Trim();
            if (s.Length == 0) return new StringValue(string.Empty);

            // String literal.
            if (s[0] == '"')
            {
                return ParseString(s);
            }

            // Hexadecimal IEEE-754 single precision float (the game's notation).
            // Must be handled before the integer / float branches.
            if (s[0] == '&')
            {
                SiiValue hexFloat = ParseHexFloat(s);
                if (hexFloat != null) return hexFloat;
            }

            // Boolean.
            if (s == "true") return new BoolValue(true);
            if (s == "false") return new BoolValue(false);

            // Vector.
            if (s[0] == '(')
            {
                return ParseVector(s);
            }

            // Integer.
            if (IntegerRegex.IsMatch(s))
            {
                long l;
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
                {
                    return new IntValue(l);
                }
            }

            // Float (decimal or scientific notation).
            if ((s.IndexOf('.') >= 0 || s.IndexOf('e') >= 0 || s.IndexOf('E') >= 0)
                && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                return new FloatValue(d);
            }

            // Reference (nameless id, possibly quoted).
            string refId = s;
            if (refId.Length >= 2 && refId[0] == '"' && refId[refId.Length - 1] == '"')
            {
                refId = refId.Substring(1, refId.Length - 2);
            }
            return new ReferenceValue(refId);
        }

        /// <summary>
        /// Parses the game's hexadecimal float notation (<c>&amp;xxxxxxxx</c>)
        /// into a <see cref="FloatValue"/> flagged as hexadecimal.
        /// </summary>
        private static SiiValue ParseHexFloat(string s)
        {
            string hex = s.Substring(1).Trim();
            if (hex.Length == 0 || hex.Length > 8) return null;

            uint bits;
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bits))
            {
                return null;
            }

            float value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            return new FloatValue(value, true);
        }

        private static SiiValue ParseString(string s)
        {
            var sb = new StringBuilder();
            int i = 1; // skip opening quote
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    if (next == '"') { sb.Append('"'); i += 2; continue; }
                    if (next == '\\') { sb.Append('\\'); i += 2; continue; }
                    sb.Append(c);
                    i++;
                    continue;
                }
                if (c == '"')
                {
                    break; // closing quote
                }
                sb.Append(c);
                i++;
            }
            return new StringValue(sb.ToString());
        }

        /// <summary>
        /// Parses a vector / placement value. Handles the three flavours the
        /// game writes: <c>(x, y)</c>, <c>(x, y, z)</c>, <c>(w; x, y, z)</c> and
        /// the placement form <c>(x, y, z) (w; a, b, c)</c>.
        /// </summary>
        private static SiiValue ParseVector(string s)
        {
            int pos = 0;
            VectorValue group = ParseVectorGroup(s, ref pos);
            if (group == null)
            {
                return new ReferenceValue(s);
            }

            while (pos < s.Length && char.IsWhiteSpace(s[pos]))
            {
                pos++;
            }

            if (pos < s.Length && s[pos] == '(')
            {
                VectorValue rotation = ParseVectorGroup(s, ref pos);
                if (rotation != null)
                {
                    group.Rotation = rotation;
                }
            }

            return group;
        }

        /// <summary>Parses one parenthesised group starting at <paramref name="pos"/>.</summary>
        private static VectorValue ParseVectorGroup(string s, ref int pos)
        {
            if (pos >= s.Length || s[pos] != '(') return null;

            int close = FindClosingParen(s, pos);
            if (close < 0) return null;

            string inner = s.Substring(pos + 1, close - pos - 1);
            pos = close + 1;

            var components = new List<SiiValue>();
            string firstSeparator = ", ";

            int semicolon = FindTopLevel(inner, ';');
            if (semicolon >= 0)
            {
                string head = inner.Substring(0, semicolon);
                string tail = inner.Substring(semicolon + 1);
                firstSeparator = "; ";
                components.Add(ParseNumber(head.Trim()));
                foreach (string part in SplitTopLevel(tail, ','))
                {
                    components.Add(ParseNumber(part.Trim()));
                }
            }
            else
            {
                foreach (string part in SplitTopLevel(inner, ','))
                {
                    components.Add(ParseNumber(part.Trim()));
                }
            }

            return new VectorValue(components) { FirstSeparator = firstSeparator };
        }

        /// <summary>Parses a single numeric component of a vector.</summary>
        private static SiiValue ParseNumber(string text)
        {
            string s = text.Trim();
            if (s.Length == 0) return new StringValue(string.Empty);

            if (s[0] == '&')
            {
                SiiValue hexFloat = ParseHexFloat(s);
                if (hexFloat != null) return hexFloat;
            }

            if (IntegerRegex.IsMatch(s) &&
                long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
            {
                return new IntValue(l);
            }

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                return new FloatValue(d);
            }

            return new ReferenceValue(s);
        }

        /// <summary>Returns the index of the parenthesis closing the one at <paramref name="open"/>.</summary>
        private static int FindClosingParen(string s, int open)
        {
            int depth = 0;
            bool inString = false;

            for (int i = open; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }

            return -1;
        }

        /// <summary>Finds a character that is not nested inside parentheses or quotes.</summary>
        private static int FindTopLevel(string s, char target)
        {
            int depth = 0;
            bool inString = false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (c == target && depth == 0) return i;
            }

            return -1;
        }

        /// <summary>Splits a string on a separator that is not nested.</summary>
        private static List<string> SplitTopLevel(string s, char separator)
        {
            var result = new List<string>();
            int depth = 0;
            bool inString = false;
            int start = 0;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (c == separator && depth == 0)
                {
                    result.Add(s.Substring(start, i - start));
                    start = i + 1;
                }
            }

            result.Add(s.Substring(start));
            return result;
        }
    }
}
