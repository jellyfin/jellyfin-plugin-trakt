using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Collection;

public class TraktMovieCollected : TraktMovie
{
    [JsonPropertyName("collected_at")]
    public string CollectedAt { get; set; }

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
