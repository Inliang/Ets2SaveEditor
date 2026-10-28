using System.Collections.Generic;
using Ets2SaveEditor.Core.Sii.Model;

namespace Ets2SaveEditor.Core.Domain
{
    /// <summary>
    /// Minimal abstraction over a collection of SII units, used by the typed
    /// wrapper classes so they do not depend directly on <see cref="SiiDocument"/>.
    /// </summary>
    public interface IUnitCollection
    {
        /// <summary>Returns the unit with the given id, or null.</summary>
        SiiUnit GetUnit(string id);

        /// <summary>Enumerates all units of the given type.</summary>
        IEnumerable<SiiUnit> UnitsOfType(string type);
    }

    /// <summary>
    /// Default <see cref="IUnitCollection"/> implementation backed by an
    /// <see cref="SiiDocument"/>.
    /// </summary>
    public class SiiDocumentCollection : IUnitCollection
    {
        private readonly SiiDocument _doc;

        public SiiDocumentCollection(SiiDocument doc)
        {
            _doc = doc;
        }

        public SiiUnit GetUnit(string id) => _doc?.GetUnitById(id);

        public IEnumerable<SiiUnit> UnitsOfType(string type) =>
            _doc?.GetUnitsByType(type) ?? new List<SiiUnit>();
    }
}
