using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Scrobble
{
    /// <summary>
    /// The trakt.tv scrobble response class.
    /// </summary>
    public class TraktScrobbleResponse
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the action.
        /// </summary>
        [JsonPropertyName("action")]
        public string Action { get; set; }

        /// <summary>
        /// Gets or sets the progress.
        /// </summary>
        [JsonPropertyName("progress")]
        public float Progress { get; set; }

        /// <summary>
        /// Gets or sets the sharing options.
        /// </summary>
        [JsonPropertyName("sharing")]
        public SocialMedia Sharing { get; set; }

        /// <summary>
        /// Gets or sets the movie.
        /// </summary>
        [JsonPropertyName("movie")]
        public TraktMovie Movie { get; set; }

        /// <summary>
        /// Gets or sets the episode.
        /// </summary>
        [JsonPropertyName("episode")]
        public TraktEpisode Episode { get; set; }

        /// <summary>
        /// Gets or sets the show.
        /// </summary>
        [JsonPropertyName("show")]
        public TraktShow Show { get; set; }
    }
}
