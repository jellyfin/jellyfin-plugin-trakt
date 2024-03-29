using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Ratings;

/// <summary>
/// The trakt.tv sync movie rated class.
/// </summary>
public class TraktMovieRated : TraktRated
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public TraktMovieId Ids { get; set; }
}
