using System;
using System.Collections.Generic;
using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Sii.Model
{    /// <summary>
    /// Top-level SII document (the content between the outer braces).
    /// </summary>
    public class SiiDocument
    {
        /// <summary>Document header, normally "SiiNunit".</summary>
        public string Header { get; set; }

        /// <summary>Ordered list of units.</summary>
        public List<SiiUnit> Units { get; set; }

        /// <summary>
        /// Schema of the document: it tells which attributes of which unit type
        /// are arrays. Needed to tell an empty array (<c>name: 0</c>) apart
        /// from an ordinary integer attribute.
        /// </summary>
        public SiiSchema Schema { get; set; }

        public SiiDocument()
        {
            Header = "SiiNunit";
            Units = new List<SiiUnit>();
        }

        /// <summary>Returns the unit with the given id, or null.</summary>
        public SiiUnit GetUnitById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var u in Units)
            {
                if (u != null && u.Id == id) return u;
            }
            return null;
        }

        /// <summary>Returns all units of the given type.</summary>
        public List<SiiUnit> GetUnitsByType(string type)
        {
            var result = new List<SiiUnit>();
            if (string.IsNullOrEmpty(type)) return result;
            foreach (var u in Units)
            {
                if (u != null && u.Type == type) result.Add(u);
            }
            return result;
        }

        /// <summary>Returns the first unit of the given type, or null.</summary>
        public SiiUnit GetFirstByType(string type)
        {
            if (string.IsNullOrEmpty(type)) return null;
            foreach (var u in Units)
            {
                if (u != null && u.Type == type) return u;
            }
            return null;
        }
    }
}
