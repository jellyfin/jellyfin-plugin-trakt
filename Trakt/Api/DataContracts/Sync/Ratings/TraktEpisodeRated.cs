using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Ratings;

public class TraktEpisodeRated : TraktRated
{
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("ids")]
    public TraktEpisodeId Ids { get; set; }
}
