using System.Collections.Generic;

namespace Ets2SaveEditor.Core.GameData
{
    /// <summary>
    /// Provides access to static game data (cities, cargoes, companies).
    /// Implementations may be built-in or backed by mods / DLC definitions.
    /// </summary>
    public interface IGameDataProvider
    {
        /// <summary>All known cities.</summary>
        List<City> Cities();

        /// <summary>All known cargo types.</summary>
        List<Cargo> Cargoes();

        /// <summary>All known companies.</summary>
        List<Company> Companies();
    }
}
