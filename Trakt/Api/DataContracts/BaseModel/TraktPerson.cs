using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

public class TraktPerson
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("ids")]
    public TraktPersonId Ids { get; set; }
}
