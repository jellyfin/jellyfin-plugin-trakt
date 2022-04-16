using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Scrobble
{
    /// <summary>
    /// The trakt.tv episode scrobble class.
    /// </summary>
    public class TraktScrobbleEpisode
    {
        /// <summary>
        /// Gets or sets the show.
        /// </summary>
        [JsonPropertyName("show")]
        public TraktShow Show { get; set; }

        /// <summary>
        /// Gets or sets the episode.
        /// </summary>
        [JsonPropertyName("episode")]
        public TraktEpisode Episode { get; set; }

        /// <summary>
        /// Gets or sets the progress.
        /// </summary>
        [JsonPropertyName("progress")]
        public float Progress { get; set; }

        /// <summary>
        /// Gets or sets the app version.
        /// </summary>
        [JsonPropertyName("app_version")]
        public string AppVersion { get; set; }

        /// <summary>
        /// Gets or sets the app date.
        /// </summary>
        [JsonPropertyName("app_date")]
        public string AppDate { get; set; }
    }
}
