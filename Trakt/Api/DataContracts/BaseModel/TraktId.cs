using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

/// <summary>
/// The trakt.tv id class.
/// </summary>
public class TraktId
{
    /// <summary>
    /// Gets or sets the trakt.tv item id.
    /// </summary>
    [JsonPropertyName("trakt")]
    public int? Trakt { get; set; }

    /// <summary>
    /// Gets or sets the item slug.
    /// </summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}
