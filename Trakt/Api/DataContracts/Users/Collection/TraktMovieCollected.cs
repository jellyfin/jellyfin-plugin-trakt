using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Collection;

/// <summary>
/// The trakt.tv users movie collected class.
/// </summary>
public class TraktMovieCollected
{
    /// <summary>
    /// Gets or sets the last collection date.
    /// </summary>
    [JsonPropertyName("collected_at")]
    public string CollectedAt { get; set; }

    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public TraktMetadata Metadata { get; set; }

    /// <summary>
    /// Gets or sets the movie.
    /// </summary>
    [JsonPropertyName("movie")]
    public TraktMovie Movie { get; set; }

    /// <summary>
    /// Gets or sets the updated date.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; }
}
