using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched;

/// <summary>
/// The trakt.tv sync movie watched class.
/// </summary>
public class TraktMovieWatched : TraktMovie
{
    /// <summary>
    /// Gets or sets the watched date.
    /// </summary>
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }
}
