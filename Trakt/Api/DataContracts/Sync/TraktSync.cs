#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync;

public class TraktSync<TMovie, TShow, TEpisode>
{
    [JsonPropertyName("movies")]
    public List<TMovie> Movies { get; set; }

    [JsonPropertyName("shows")]
    public List<TShow> Shows { get; set; }

    [JsonPropertyName("episodes")]
    public List<TEpisode> Episodes { get; set; }
}
