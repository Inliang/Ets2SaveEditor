using System;
using System.Collections.Generic;

namespace Ets2SaveEditor.Core.Sii
{
    /// <summary>
    /// Describes which attributes of a unit type are stored as arrays.
    /// <para>
    /// The SII text format writes an array as a header line holding the element
    /// count followed by one indexed line per element:
    /// <code>
    /// name: 3
    /// name[0]: a
    /// name[1]: b
    /// name[2]: c
    /// </code>
    /// An <em>empty</em> array therefore consists of the header line alone
    /// (<c>name: 0</c>) and is textually indistinguishable from an ordinary
    /// integer attribute. Only the schema of the save file tells the two
    /// apart, which is why the game keeps the definitions in its own
    /// <c>.sii</c> definition files.
    /// </para>
    /// <para>
    /// This class carries that knowledge:
    /// <list type="bullet">
    ///   <item><description><see cref="IsArrayField"/> answers whether a
    ///   given attribute of a given unit type is an array.</description></item>
    ///   <item><description>The schema is extracted from the structure
    ///   definitions embedded in binary (BSII) saves, which is authoritative;
    ///   for plaintext saves the built-in table of the modern save format is
    ///   used as a fallback.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class SiiSchema
    {
        private readonly Dictionary<string, HashSet<string>> _arrayFields;

        /// <summary>An empty schema that knows about no arrays at all.</summary>
        public static readonly SiiSchema Empty = new SiiSchema(
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));

        /// <summary>
        /// Creates a schema from a map of unit type to the names of its array
        /// attributes.
        /// </summary>
        public SiiSchema(Dictionary<string, HashSet<string>> arrayFields)
        {
            _arrayFields = arrayFields ?? new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        }

        /// <summary>Number of unit types described by this schema.</summary>
        public int UnitTypeCount => _arrayFields.Count;

        /// <summary>
        /// Returns whether <paramref name="fieldName"/> is an array attribute of
        /// the unit type <paramref name="unitType"/>.
        /// </summary>
        public bool IsArrayField(string unitType, string fieldName)
        {
            if (string.IsNullOrEmpty(unitType) || string.IsNullOrEmpty(fieldName)) return false;
            return _arrayFields.TryGetValue(unitType, out HashSet<string> fields) &&
                   fields.Contains(fieldName);
        }

        /// <summary>
        /// Returns the names of all array attributes of the given unit type
        /// (never null).
        /// </summary>
        public IReadOnlyCollection<string> GetArrayFields(string unitType)
        {
            if (string.IsNullOrEmpty(unitType)) return Array.Empty<string>();
            return _arrayFields.TryGetValue(unitType, out HashSet<string> fields)
                ? fields
                : Array.Empty<string>();
        }

        /// <summary>
        /// Returns every unit type described by this schema together with its
        /// array attributes.
        /// </summary>
        public IEnumerable<KeyValuePair<string, IReadOnlyCollection<string>>> GetAll()
        {
            foreach (var kv in _arrayFields)
            {
                yield return new KeyValuePair<string, IReadOnlyCollection<string>>(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Merges another schema into this one. Fields of the other schema win
        /// when both describe the same unit type, because the more specific
        /// (binary) definition is the authoritative one.
        /// </summary>
        public SiiSchema Merge(SiiSchema other)
        {
            if (other == null) return this;

            var merged = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in _arrayFields)
            {
                merged[kv.Key] = new HashSet<string>(kv.Value, StringComparer.Ordinal);
            }

            foreach (var kv in other._arrayFields)
            {
                if (merged.TryGetValue(kv.Key, out HashSet<string> existing))
                {
                    existing.UnionWith(kv.Value);
                }
                else
                {
                    merged[kv.Key] = new HashSet<string>(kv.Value, StringComparer.Ordinal);
                }
            }

            return new SiiSchema(merged);
        }
    }
}
