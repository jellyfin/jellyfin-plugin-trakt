using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Scrobble
{
    /// <summary>
    /// The trakt.tv movie scrobble class.
    /// </summary>
    public class TraktScrobbleMovie
    {
        /// <summary>
        /// Gets or sets the movie.
        /// </summary>
        [JsonPropertyName("movie")]
        public TraktMovie Movie { get; set; }

        /// <summary>
        /// Gets or sets the progress.
        /// </summary>
        [JsonPropertyName("progress")]
        public float Progress { get; set; }

        /// <summary>
        /// Gets or sets the app versin.
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
