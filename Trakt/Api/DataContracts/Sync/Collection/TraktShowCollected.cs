#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Collection;

public class TraktShowCollected : TraktShow
{
    [JsonPropertyName("seasons")]
    public List<TraktSeasonCollected> Seasons { get; set; }
}
