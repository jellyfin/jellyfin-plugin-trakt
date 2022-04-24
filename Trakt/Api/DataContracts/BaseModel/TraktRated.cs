using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

/// <summary>
/// The trakt.tv rated class.
/// </summary>
public abstract class TraktRated
{
    /// <summary>
    /// Gets or sets the rating.
    /// </summary>
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    /// <summary>
    /// Gets or sets the rating date.
    /// </summary>
    [JsonPropertyName("rated_at")]
    public string RatedAt { get; set; }
}
