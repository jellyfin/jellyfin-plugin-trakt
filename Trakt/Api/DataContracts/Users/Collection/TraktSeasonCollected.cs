#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users.Collection
{
    /// <summary>
    /// The trakt.tv users season collected class.
    /// </summary>
    public class TraktSeasonCollected
    {
        /// <summary>
        /// Gets or sets the season unumber.
        /// </summary>
        [JsonPropertyName("number")]
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the episodes.
        /// </summary>
        [JsonPropertyName("episodes")]
        public IReadOnlyList<TraktEpisodeCollected> Episodes { get; set; }
    }
}
