using System;
using System.Collections.Generic;

namespace Ets2SaveEditor.Core.Sii.Model
{
    /// <summary>
    /// A generic SII unit (lossless representation). Unknown fields are preserved
    /// in <see cref="RawLines"/> so that saving never corrupts the save file.
    /// </summary>
    public class SiiUnit
    {
        /// <summary>Unit type (e.g. "bank", "garage").</summary>
        public string Type { get; set; }

        /// <summary>Unit identifier (e.g. "_nameless.000").</summary>
        public string Id { get; set; }

        /// <summary>Parsed attributes.</summary>
        public List<SiiAttribute> Attributes { get; set; }

        /// <summary>Lines that could not be parsed / nested structures, kept verbatim.</summary>
        public List<string> RawLines { get; set; }

        public SiiUnit()
        {
            Type = string.Empty;
            Id = string.Empty;
            Attributes = new List<SiiAttribute>();
            RawLines = new List<string>();
        }

        public SiiUnit(string type, string id) : this()
        {
            Type = type ?? string.Empty;
            Id = id ?? string.Empty;
        }

        /// <summary>Returns the attribute with the given name, or null if not found.</summary>
        public SiiAttribute GetAttribute(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var a in Attributes)
            {
                if (a != null && a.Name == name) return a;
            }
            return null;
        }

        /// <summary>Replaces an existing attribute value or adds a new one.</summary>
        public void SetAttribute(string name, SiiValue value)
        {
            if (string.IsNullOrEmpty(name)) return;
            var existing = GetAttribute(name);
            if (existing != null)
            {
                existing.Value = value;
                existing.IsArray = false;
            }
            else
            {
                Attributes.Add(new SiiAttribute(name, value));
            }
        }

        /// <summary>Removes the attribute with the given name.</summary>
        public void RemoveAttribute(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            Attributes.RemoveAll(a => a != null && a.Name == name);
        }

        public string GetString(string name)
        {
            var a = GetAttribute(name);
            if (a != null && a.Value is StringValue sv) return sv.Value;
            return string.Empty;
        }

        public long GetLong(string name)
        {
            var a = GetAttribute(name);
            if (a != null && a.Value is IntValue iv) return iv.Value;
            return 0;
        }

        public double GetDouble(string name)
        {
            var a = GetAttribute(name);
            if (a != null)
            {
                if (a.Value is FloatValue fv) return fv.Value;
                if (a.Value is IntValue iv) return iv.Value;
            }
            return 0;
        }

        public bool GetBool(string name)
        {
            var a = GetAttribute(name);
            if (a != null && a.Value is BoolValue bv) return bv.Value;
            return false;
        }

        public string GetReference(string name)
        {
            var a = GetAttribute(name);
            if (a != null && a.Value is ReferenceValue rv) return rv.TargetId;
            return string.Empty;
        }

        // ---- SetScalar overloads (write typed values into attributes) ----

        public void SetScalar(string name, long value) => SetAttribute(name, new IntValue(value));

        public void SetScalar(string name, long? value) => SetAttribute(name, new IntValue(value ?? 0));

        public void SetScalar(string name, double value) => SetAttribute(name, new FloatValue(value));

        public void SetScalar(string name, bool value) => SetAttribute(name, new BoolValue(value));

        public void SetScalar(string name, string value) => SetAttribute(name, new StringValue(value));

        public void SetScalar(string name, ReferenceValue value) => SetAttribute(name, value);
    }
}
