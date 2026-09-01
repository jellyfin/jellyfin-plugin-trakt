using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync.LastActivities;

/// <summary>
/// The trakt.tv sync last activities class.
/// </summary>
public class TraktSyncLastActivities
{
    /// <summary>
    /// Gets or sets the most recent activity date across all media types.
    /// </summary>
    [JsonPropertyName("all")]
    public string All { get; set; }

    /// <summary>
    /// Gets or sets the movie last activities.
    /// </summary>
    [JsonPropertyName("movies")]
    public TraktMovieLastActivities Movies { get; set; }

    /// <summary>
    /// Gets or sets the episode last activities.
    /// </summary>
    [JsonPropertyName("episodes")]
    public TraktEpisodeLastActivities Episodes { get; set; }

    /// <summary>
    /// Gets or sets the show last activities.
    /// </summary>
    [JsonPropertyName("shows")]
    public TraktShowLastActivities Shows { get; set; }
}
