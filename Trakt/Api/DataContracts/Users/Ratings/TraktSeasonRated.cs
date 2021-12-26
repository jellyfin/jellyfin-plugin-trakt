using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings;

public class TraktSeasonRated : TraktRated
{
    [JsonPropertyName("season")]
    public TraktSeason Season { get; set; }
}
