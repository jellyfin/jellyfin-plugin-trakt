using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync.LastActivities;

/// <summary>
/// The trakt.tv show last activities class.
/// </summary>
public class TraktShowLastActivities
{
    /// <summary>
    /// Gets or sets the date a show was last hidden or unhidden. Progress resets
    /// surface here rather than bumping the watched dates.
    /// </summary>
    [JsonPropertyName("hidden_at")]
    public string HiddenAt { get; set; }
}
