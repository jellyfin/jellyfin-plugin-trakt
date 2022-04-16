using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings
{
    /// <summary>
    /// The trakt.tv users movie rated class.
    /// </summary>
    public class TraktMovieRated : TraktRated
    {
        /// <summary>
        /// Gets or sets the movie.
        /// </summary>
        [JsonPropertyName("movie")]
        public TraktMovie Movie { get; set; }
    }
}
