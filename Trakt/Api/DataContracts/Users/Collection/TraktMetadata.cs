using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users.Collection;

public class TraktMetadata
{
    [JsonPropertyName("media_type")]
    public string MediaType { get; set; }

    [JsonPropertyName("resolution")]
    public string Resolution { get; set; }

    [JsonPropertyName("audio")]
    public string Audio { get; set; }

    [JsonPropertyName("audio_channels")]
    public string AudioChannels { get; set; }

    // public bool 3d { get; set; }
}
