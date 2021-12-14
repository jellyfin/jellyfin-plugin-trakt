using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public class TraktPersonId : TraktIMDBandTMDBId
{
    [JsonPropertyName("tvrage")]
    public int? Tvrage { get; set; }
}
