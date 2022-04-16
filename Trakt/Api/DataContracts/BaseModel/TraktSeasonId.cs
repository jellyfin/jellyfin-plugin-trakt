using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel
{
    /// <summary>
    /// The trakt.tv season id class.
    /// </summary>
    public class TraktSeasonId : TraktId
    {
        /// <summary>
        /// Gets or sets the season TMDb id.
        /// </summary>
        [JsonPropertyName("tmdb")]
        public int? Tmdb { get; set; }

        /// <summary>
        /// Gets or sets the season TVDb id.
        /// </summary>
        [JsonPropertyName("tvdb")]
        public int? Tvdb { get; set; }

        /// <summary>
        /// Gets or sets the season TVRage id.
        /// </summary>
        [JsonPropertyName("tvrage")]
        public int? Tvrage { get; set; }
    }
}
