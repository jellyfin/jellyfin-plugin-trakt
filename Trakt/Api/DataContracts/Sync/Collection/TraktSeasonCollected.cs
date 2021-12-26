#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Sync.Collection;

public class TraktSeasonCollected
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("episodes")]
    public List<TraktEpisodeCollected> Episodes { get; set; }
}
