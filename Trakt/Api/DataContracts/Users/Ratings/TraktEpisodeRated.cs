using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings;

public class TraktEpisodeRated : TraktRated
{
    [JsonPropertyName("episode")]
    public TraktEpisode Episode { get; set; }
}
