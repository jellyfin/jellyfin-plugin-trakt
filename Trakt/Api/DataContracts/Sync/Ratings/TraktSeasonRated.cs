#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Ratings;

/// <summary>
/// The trakt.tv sync season rated class.
/// </summary>
public class TraktSeasonRated : TraktRated
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public IReadOnlyList<TraktEpisodeRated> Episodes { get; set; }
}
