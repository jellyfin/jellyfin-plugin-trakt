#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Collection;

public class TraktShowCollected
{
    [JsonPropertyName("last_collected_at")]
    public string LastCollectedAt { get; set; }

    [JsonPropertyName("show")]
    public TraktShow Show { get; set; }

    [JsonPropertyName("seasons")]
    public List<TraktSeasonCollected> Seasons { get; set; }
}
