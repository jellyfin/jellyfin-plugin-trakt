#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched
{
    /// <summary>
    /// The trakt.tv sync season watched class.
    /// </summary>
    public class TraktSeasonWatched : TraktSeason
    {
        /// <summary>
        /// Gets or sets the watched date.
        /// </summary>
        [JsonPropertyName("watched_at")]
        public string WatchedAt { get; set; }

        /// <summary>
        /// Gets or sets the episodes.
        /// </summary>
        [JsonPropertyName("episodes")]
        public ICollection<TraktEpisodeWatched> Episodes { get; set; }
    }
}
