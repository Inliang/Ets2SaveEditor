using System.Collections.Generic;
using Ets2SaveEditor.Core.Common;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain.TypedUnits
{
    /// <summary>
    /// Typed wrapper around the <c>economy</c> unit.
    /// </summary>
    public class Economy
    {
        private readonly SiiUnit _unit;

        /// <summary>The underlying SII unit.</summary>
        public SiiUnit Unit => _unit;

        public Economy(SiiUnit unit)
        {
            _unit = unit;
        }

        /// <summary>
        /// Cities the player has visited. Best-effort: reads the "visited_cities"
        /// attribute (array of references or strings). Returns an empty list if
        /// the field is absent or its exact shape is unknown.
        /// </summary>
        public List<string> VisitedCities
        {
            get
            {
                var list = new List<string>();
                if (_unit == null) return list;

                var attr = _unit.GetAttribute("visited_cities");
                if (attr == null) return list;

                if (attr.IsArray && attr.Value is ArrayValue arr)
                {
                    foreach (var item in arr.Items)
                    {
                        if (item is ReferenceValue rv) list.Add(rv.TargetId);
                        else if (item is StringValue sv) list.Add(sv.Value);
                    }
                }
                else if (attr.Value is ReferenceValue rv2)
                {
                    list.Add(rv2.TargetId);
                }
                else if (attr.Value is StringValue sv2)
                {
                    list.Add(sv2.Value);
                }

                return list;
            }
        }

        public static Economy FromDocument(SiiDocument doc)
        {
            if (doc == null) return null;
            var unit = doc.GetFirstByType(Constants.EconomyType);
            if (unit == null) return null;
            return new Economy(unit);
        }
    }
}
