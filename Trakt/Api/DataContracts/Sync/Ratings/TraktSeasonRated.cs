#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Ratings;

public class TraktSeasonRated : TraktRated
{
    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("episodes")]
    public List<TraktEpisodeRated> Episodes { get; set; }
}
