using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public class TraktIMDBandTMDBId : TraktId
{
    [JsonPropertyName("imdb")]
    public string Imdb { get; set; }

    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }
}
