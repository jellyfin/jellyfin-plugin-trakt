#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync;

/// <summary>
/// The trakt.tv sync episode not found class.
/// An episode sent with episode ids comes back as that episode. An episode sent by show, season and number
/// comes back as the show that was sent, with the show title, year and ids and the episodes in <see cref="Seasons"/>.
/// </summary>
public class TraktEpisodeNotFound
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("season")]
    public int Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    [JsonPropertyName("number")]
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the episode title, or the show title if <see cref="Seasons"/> is set.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the episode ids, or the show ids if <see cref="Seasons"/> is set.
    /// </summary>
    [JsonPropertyName("ids")]
    public TraktTVId Ids { get; set; }

    /// <summary>
    /// Gets or sets the show year.
    /// </summary>
    [JsonPropertyName("year")]
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the show seasons with the episodes that were not found.
    /// </summary>
    [JsonPropertyName("seasons")]
    public IReadOnlyList<TraktSeasonNotFound> Seasons { get; set; }
}
