using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.Enums;

namespace Trakt.Api.DataContracts.Sync.Collection;

/// <summary>
/// The trakt.tv sync movies collected class.
/// </summary>
public class TraktMovieCollected : TraktMovie
{
    /// <summary>
    /// Gets or sets the collection date.
    /// </summary>
    [JsonPropertyName("collected_at")]
    public string CollectedAt { get; set; }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    [JsonPropertyName("media_type")]
    public TraktMediaType? MediaType { get; set; }

    /// <summary>
    /// Gets or sets the resolution.
    /// </summary>
    [JsonPropertyName("resolution")]
    public TraktResolution? Resolution { get; set; }

    /// <summary>
    /// Gets or sets the audio.
    /// </summary>
    [JsonPropertyName("audio")]
    public TraktAudio? Audio { get; set; }

    /// <summary>
    /// Gets or sets the amount of audio channels.
    /// </summary>
    [JsonPropertyName("audio_channels")]
    public string AudioChannels { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the movie is 3D.
    /// </summary>
    [JsonPropertyName("3d")]
    public bool Is3D { get; set; } = false;

    /// <summary>
    /// Gets or sets the HDR type.
    /// </summary>
    [JsonPropertyName("hdr")]
    public TraktHdr? Hdr { get; set; }
}
