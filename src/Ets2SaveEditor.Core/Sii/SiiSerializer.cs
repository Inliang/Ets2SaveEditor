using System;
using System.Text;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>
    /// Serializes a <see cref="SiiDocument"/> back to plaintext SII text.
    /// Parsed attributes are regenerated from their values; <see cref="SiiUnit.RawLines"/>
    /// are emitted verbatim so unknown structures survive a round-trip untouched.
    /// </summary>
    public static class SiiSerializer
    {
        /// <summary>
        /// Serializes the document to SII text.
        /// </summary>
        /// <exception cref="ArgumentNullException">When doc is null.</exception>
        public static string Serialize(SiiDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var sb = new StringBuilder();
            sb.AppendLine(doc.Header ?? Constants.SiiHeader);
            sb.AppendLine("{");

            foreach (var unit in doc.Units)
            {
                if (unit == null) continue;

                sb.AppendLine(unit.Type + " : " + unit.Id + " {");

                foreach (var attr in unit.Attributes)
                {
                    if (attr == null) continue;

                    if (attr.IsArray && attr.Value is ArrayValue arr)
                    {
                        foreach (var item in arr.Items)
                        {
                            sb.AppendLine("  " + attr.Name + " : " + (item != null ? item.ToSiiString() : string.Empty));
                        }
                    }
                    else
                    {
                        sb.AppendLine("  " + attr.Name + " : " + (attr.Value != null ? attr.Value.ToSiiString() : string.Empty));
                    }
                }

                foreach (var raw in unit.RawLines)
                {
                    sb.AppendLine(raw);
                }

                sb.AppendLine("}");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
