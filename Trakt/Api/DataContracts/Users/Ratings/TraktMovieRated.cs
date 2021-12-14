using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings;

public class TraktMovieRated : TraktRated
{
    [JsonPropertyName("movie")]
    public TraktMovie Movie { get; set; }
}
