using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public class TraktMovie
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("ids")]
    public TraktMovieId Ids { get; set; }
}
