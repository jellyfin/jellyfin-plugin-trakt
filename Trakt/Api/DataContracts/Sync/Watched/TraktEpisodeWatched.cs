using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched
{
    /// <summary>
    /// The trakt.tv sync episode watched class.
    /// </summary>
    public class TraktEpisodeWatched : TraktEpisode
    {
        /// <summary>
        /// Gets or sets the watched date.
        /// </summary>
        [JsonPropertyName("watched_at")]
        public string WatchedAt { get; set; }
    }
}
