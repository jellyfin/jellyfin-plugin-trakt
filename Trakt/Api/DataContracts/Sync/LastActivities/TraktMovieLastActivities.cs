using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync.LastActivities;

/// <summary>
/// The trakt.tv movie last activities class.
/// </summary>
public class TraktMovieLastActivities
{
    /// <summary>
    /// Gets or sets the date the watched history was last changed.
    /// </summary>
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the date the playback progress was last changed.
    /// </summary>
    [JsonPropertyName("paused_at")]
    public string PausedAt { get; set; }
}
