using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.BaseModel;

/// <summary>
/// The trakt.tv show class.
/// </summary>
public class TraktShow
{
    /// <summary>
    /// Gets or sets the show title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the show year.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the show ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public TraktShowId Ids { get; set; }
}
