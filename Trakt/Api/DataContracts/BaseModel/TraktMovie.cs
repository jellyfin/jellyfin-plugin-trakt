using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel
{
    /// <summary>
    /// The trakt.tv movie class.
    /// </summary>
    public class TraktMovie
    {
        /// <summary>
        /// Gets or sets the movie title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the movie year.
        /// </summary>
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the movie ids.
        /// </summary>
        [JsonPropertyName("ids")]
        public TraktMovieId Ids { get; set; }
    }
}
