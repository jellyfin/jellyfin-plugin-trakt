using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync;

public class Items
{
    [JsonPropertyName("movies")]
    public int Movies { get; set; }

    [JsonPropertyName("shows")]
    public int Shows { get; set; }

    [JsonPropertyName("seasons")]
    public int Seasons { get; set; }

    [JsonPropertyName("episodes")]
    public int Episodes { get; set; }

    [JsonPropertyName("people")]
    public int People { get; set; }
}
