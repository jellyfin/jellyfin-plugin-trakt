using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users.Collection;

public class TraktEpisodeCollected
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("collected_at")]
    public string CollectedAt { get; set; }

    [JsonPropertyName("metadata")]
    public TraktMetadata Metadata { get; set; }
}
