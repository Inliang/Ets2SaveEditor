using System;
using System.Collections.Generic;

namespace Ets2SaveEditor.Core.GameData
{
    /// <summary>
    /// A city the player can visit.
    /// <para>
    /// The game stores the cities a player has discovered in the
    /// <c>economy.visited_cities</c> array (332 entries in a typical
    /// European save). Adding a city to that array makes the city available
    /// in the freight market and the job list — this is the mechanism the
    /// reference tool TS-SE-Tool uses for its "visit cities" feature.
    /// </para>
    /// </summary>
    public class VisitedCity
    {
        /// <summary>Internal token used by the game (e.g. "london").</summary>
        public string Token { get; set; }

        /// <summary>Human-readable name (when known).</summary>
        public string Name { get; set; }

        /// <summary>Whether the player has already visited the city.</summary>
        public bool Visited { get; set; }

        public VisitedCity()
        {
            Token = string.Empty;
            Name = string.Empty;
        }

        public VisitedCity(string token, string name, bool visited)
        {
            Token = token ?? string.Empty;
            Name = name ?? string.Empty;
            Visited = visited;
        }

        /// <summary>
        /// Returns the display name: the human-readable name when known,
        /// otherwise the raw token.
        /// </summary>
        public string DisplayName => string.IsNullOrEmpty(Name) ? Token : Name;

        public override string ToString() => DisplayName;
    }
}
