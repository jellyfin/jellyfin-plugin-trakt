using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public abstract class TraktRated
{
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    [JsonPropertyName("rated_at")]
    public string RatedAt { get; set; }
}
