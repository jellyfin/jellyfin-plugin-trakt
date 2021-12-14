using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync;

public class TraktSyncResponse
{
    [JsonPropertyName("added")]
    public Items Added { get; set; }

    [JsonPropertyName("deleted")]
    public Items Deleted { get; set; }

    [JsonPropertyName("existing")]
    public Items Existing { get; set; }

    [JsonPropertyName("not_found")]
    public NotFoundObjects NotFound { get; set; }
}
