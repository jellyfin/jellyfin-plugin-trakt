#pragma warning disable CA2227

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync;

/// <summary>
/// The trakt.tv sync class.
/// </summary>
/// <typeparam name="TMovie">The type of the movie.</typeparam>
/// <typeparam name="TShow">The type of the show.</typeparam>
/// <typeparam name="TEpisode">The type of the episode.</typeparam>
public class TraktSync<TMovie, TShow, TEpisode>
{
    /// <summary>
    /// Gets or sets the movies.
    /// </summary>
    [JsonPropertyName("movies")]
    public ICollection<TMovie> Movies { get; set; }

    /// <summary>
    /// Gets or sets the shows.
    /// </summary>
    [JsonPropertyName("shows")]
    public ICollection<TShow> Shows { get; set; }

    /// <summary>
    /// Gets or sets the episodes.
    /// </summary>
    [JsonPropertyName("episodes")]
    public ICollection<TEpisode> Episodes { get; set; }
}
