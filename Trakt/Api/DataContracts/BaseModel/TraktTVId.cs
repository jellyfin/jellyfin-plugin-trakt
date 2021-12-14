using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public class TraktTVId : TraktIMDBandTMDBId
{
    [JsonPropertyName("tvdb")]
    public int? Tvdb { get; set; }

    [JsonPropertyName("tvrage")]
    public int? Tvrage { get; set; }
}
