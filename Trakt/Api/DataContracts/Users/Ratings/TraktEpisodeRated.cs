using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings
{
    /// <summary>
    /// The trakt.tv users rating class.
    /// </summary>
    public class TraktEpisodeRated : TraktRated
    {
        /// <summary>
        /// Gets or sets the episode.
        /// </summary>
        [JsonPropertyName("episode")]
        public TraktEpisode Episode { get; set; }
    }
}
