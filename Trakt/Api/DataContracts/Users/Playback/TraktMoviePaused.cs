using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Playback;

/// <summary>
/// The trakt.tv movie paused class.
/// </summary>
public class TraktMoviePaused
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public TraktMovie Movie { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    [JsonPropertyName("paused_at")]
    public string PausedAt { get; set; }

    /// <summary>
    /// Gets or sets the movie year.
    /// </summary>
    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }
}
