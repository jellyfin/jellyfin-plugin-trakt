#pragma warning disable CA1002
#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched
{
    /// <summary>
    /// The trakt.tv sync show watched class.
    /// </summary>
    public class TraktShowWatched : TraktShow
    {
        /// <summary>
        /// Gets or sets the watched date.
        /// </summary>
        [JsonPropertyName("watched_at")]
        public string WatchedAt { get; set; }

        /// <summary>
        /// Gets or sets the seasons.
        /// </summary>
        [JsonPropertyName("seasons")]
        public List<TraktSeasonWatched> Seasons { get; set; }
    }
}
