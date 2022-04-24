using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

/// <summary>
/// The trakt.tv season class.
/// </summary>
public class TraktSeason
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the season ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public TraktSeasonId Ids { get; set; }
}
