using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public class TraktSeason
{
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("ids")]
    public TraktSeasonId Ids { get; set; }
}
