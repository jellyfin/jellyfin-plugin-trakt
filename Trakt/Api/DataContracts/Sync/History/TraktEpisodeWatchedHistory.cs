using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.History;

/// <summary>
/// The trakt.tv sync episode watched history class.
/// </summary>
public class TraktEpisodeWatchedHistory
{
    /// <summary>
    /// Gets or sets the watched date.
    /// </summary>
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the action.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the episode.
    /// </summary>
    [JsonPropertyName("episode")]
    public TraktEpisode Episode { get; set; }

    /// <summary>
    /// Gets or sets the episode.
    /// </summary>
    [JsonPropertyName("show")]
    public TraktShow Show { get; set; }
}
