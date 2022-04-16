using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel
{
    /// <summary>
    /// The trakt.tv IDMb and TMDb id class.
    /// </summary>
    public class TraktIMDBandTMDBId : TraktId
    {
        /// <summary>
        /// Gets or sets the IMDb id.
        /// </summary>
        [JsonPropertyName("imdb")]
        public string Imdb { get; set; }

        /// <summary>
        /// Gets or sets the TMDb id.
        /// </summary>
        [JsonPropertyName("tmdb")]
        public int? Tmdb { get; set; }
    }
}
