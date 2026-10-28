namespace Ets2SaveEditor.Core.Sii.Model
{
    /// <summary>
    /// A single attribute (name : value) within an SII unit.
    /// </summary>
    public class SiiAttribute
    {
        /// <summary>Attribute name.</summary>
        public string Name { get; set; }

        /// <summary>Parsed attribute value.</summary>
        public SiiValue Value { get; set; }

        /// <summary>Whether this attribute represents a repeated (array) value.</summary>
        public bool IsArray { get; set; }

        /// <summary>Original order index within the unit (for stable serialization).</summary>
        public int Order { get; set; }

        /// <summary>Original raw text of the line, kept for lossless passthrough.</summary>
        public string RawText { get; set; }

        public SiiAttribute()
        {
            Name = string.Empty;
            Value = null;
            IsArray = false;
            Order = 0;
            RawText = string.Empty;
        }

        public SiiAttribute(string name, SiiValue value)
        {
            Name = name ?? string.Empty;
            Value = value;
            IsArray = false;
            Order = 0;
            RawText = string.Empty;
        }
    }
}
