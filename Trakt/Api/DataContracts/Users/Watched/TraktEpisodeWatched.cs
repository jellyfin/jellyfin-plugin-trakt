using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users.Watched;

/// <summary>
/// The trakt.tv users episode watched class.
/// </summary>
public class TraktEpisodeWatched
{
    /// <summary>
    /// Gets or sets the last updated date.
    /// </summary>
    [JsonPropertyName("last_updated_at")]
    public string LastUpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last watched date.
    /// </summary>
    [JsonPropertyName("last_watched_at")]
    public string LastWatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the amount of plays.
    /// </summary>
    [JsonPropertyName("plays")]
    public int Plays { get; set; }
}
