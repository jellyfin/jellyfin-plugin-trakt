#pragma warning disable CA1002
#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync.Collection
{
    /// <summary>
    /// The trakt.tv sync seasons collected class.
    /// </summary>
    public class TraktSeasonCollected
    {
        /// <summary>
        /// Gets or sets the season number.
        /// </summary>
        [JsonPropertyName("number")]
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the episodes.
        /// </summary>
        [JsonPropertyName("episodes")]
        public List<TraktEpisodeCollected> Episodes { get; set; }
    }
}
