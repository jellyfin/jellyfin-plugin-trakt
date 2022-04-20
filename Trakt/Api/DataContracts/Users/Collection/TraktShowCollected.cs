#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Collection
{
    /// <summary>
    /// The trakt.tv users show collected class.
    /// </summary>
    public class TraktShowCollected
    {
        /// <summary>
        /// Gets or sets the last collected date.
        /// </summary>
        [JsonPropertyName("last_collected_at")]
        public string LastCollectedAt { get; set; }

        /// <summary>
        /// Gets or sets the show.
        /// </summary>
        [JsonPropertyName("show")]
        public TraktShow Show { get; set; }

        /// <summary>
        /// Gets or sets the seasons.
        /// </summary>
        [JsonPropertyName("seasons")]
        public IReadOnlyList<TraktSeasonCollected> Seasons { get; set; }
    }
}
