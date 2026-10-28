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

        private static readonly Regex AttributeRegex =
            new Regex(@"^\s*(?<name>[\w\.]+)\s*:\s*(?<val>.*)$", RegexOptions.Compiled);

        /// <summary>
        /// Parses the given SII text into a <see cref="SiiDocument"/>.
        /// </summary>
        /// <exception cref="SiiFormatException">When the text is empty or malformed.</exception>
        public static SiiDocument Parse(string text)
        {
            var doc = new SiiDocument();

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

                    if (val.Length > 0 && !val.StartsWith("{"))
                    {
                        SiiValue value = ToSiiValue(val);
                        AddAttribute(unit, name, value, line);
                        index++;
                        continue;
                    }
                }

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

            // Boolean.
            if (s == "true") return new BoolValue(true);
            if (s == "false") return new BoolValue(false);

            // Vector.
            if (s[0] == '(')
            {
                return ParseVector(s);
            }

            // Integer.
            if (Regex.IsMatch(s, @"^-?\d+$"))
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

        private static SiiValue ParseVector(string s)
        {
            int end = s.IndexOf(')');
            if (end < 0) end = s.Length;
            string inner = s.Substring(1, end - 1);
            string[] parts = inner.Split(',');
            double x = 0, y = 0, z = 0;
            if (parts.Length > 0) double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x);
            if (parts.Length > 1) double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y);
            if (parts.Length > 2) double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out z);
            return new VectorValue(x, y, z);
        }
    }
}
