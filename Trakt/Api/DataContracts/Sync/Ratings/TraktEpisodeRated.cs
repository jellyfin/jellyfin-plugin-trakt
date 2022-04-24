using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Ratings;

/// <summary>
/// The trakt.tv sync episode rated class.
/// </summary>
public class TraktEpisodeRated : TraktRated
{
    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the ids.
    /// </summary>
    [JsonPropertyName("ids")]
    public TraktEpisodeId Ids { get; set; }
}
