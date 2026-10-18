#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync;

/// <summary>
/// The trakt.tv sync season not found class.
/// </summary>
public class TraktSeasonNotFound : TraktSeason
{
    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public IReadOnlyList<TraktEpisode> Episodes { get; set; }
}
