using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public class TraktSeasonId : TraktId
{
    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }

    [JsonPropertyName("tvdb")]
    public int? Tvdb { get; set; }

    [JsonPropertyName("tvrage")]
    public int? Tvrage { get; set; }
}
