#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched;

public class TraktShowWatched : TraktShow
{
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }

    [JsonPropertyName("seasons")]
    public List<TraktSeasonWatched> Seasons { get; set; }
}
