#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync;

public class NotFoundObjects
{
    [JsonPropertyName("movies")]
    public List<TraktMovie> Movies { get; set; }

    [JsonPropertyName("shows")]
    public List<TraktShow> Shows { get; set; }

    [JsonPropertyName("episodes")]
    public List<TraktEpisode> Episodes { get; set; }

    [JsonPropertyName("seasons")]
    public List<TraktSeason> Seasons { get; set; }

    [JsonPropertyName("people")]
    public List<TraktPerson> People { get; set; }
}
