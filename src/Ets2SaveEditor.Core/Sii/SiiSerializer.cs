using System;
using System.Globalization;
using System.Text;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>
    /// Serializes a <see cref="SiiDocument"/> back to plaintext SII text.
    /// Parsed attributes are regenerated from their values; <see cref="SiiUnit.RawLines"/>
    /// are emitted verbatim so unknown structures survive a round-trip untouched.
    /// <para>
    /// The output format matches the one the game itself writes:
    /// <c>name: value</c> for attributes, <c>name : id {</c> for units,
    /// <c>name: count</c> + <c>name[i]: value</c> for arrays.
    /// </para>
    /// </summary>
    public static class SiiSerializer
    {
        private const string NewLine = "\r\n";

        /// <summary>
        /// Serializes the document to SII text.
        /// </summary>
        /// <exception cref="ArgumentNullException">When doc is null.</exception>
        public static string Serialize(SiiDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var sb = new StringBuilder();
            sb.Append(doc.Header ?? Constants.SiiHeader).Append(NewLine);
            sb.Append('{').Append(NewLine);

            foreach (var unit in doc.Units)
            {
                if (unit == null) continue;

                sb.Append(unit.Type).Append(" : ").Append(unit.Id).Append(" {").Append(NewLine);

                foreach (var attr in unit.Attributes)
                {
                    if (attr == null) continue;

                    if (attr.IsArray && attr.Value is ArrayValue arr)
                    {
                        sb.Append(' ').Append(attr.Name).Append(": ")
                          .Append(arr.Items.Count.ToString(CultureInfo.InvariantCulture)).Append(NewLine);

                        for (int i = 0; i < arr.Items.Count; i++)
                        {
                            sb.Append(' ').Append(attr.Name).Append('[')
                              .Append(i.ToString(CultureInfo.InvariantCulture)).Append("]: ")
                              .Append(arr.Items[i] != null ? arr.Items[i].ToSiiString() : string.Empty)
                              .Append(NewLine);
                        }
                    }
                    else
                    {
                        sb.Append(' ').Append(attr.Name).Append(": ")
                          .Append(attr.Value != null ? attr.Value.ToSiiString() : string.Empty)
                          .Append(NewLine);
                    }
                }

                foreach (var raw in unit.RawLines)
                {
                    sb.Append(raw).Append(NewLine);
                }

                sb.Append('}').Append(NewLine).Append(NewLine);
            }

            sb.Append('}').Append(NewLine);
            return sb.ToString();
        }
    }
}
