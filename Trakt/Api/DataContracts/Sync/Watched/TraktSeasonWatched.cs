#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched;

public class TraktSeasonWatched : TraktSeason
{
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }

    [JsonPropertyName("episodes")]
    public List<TraktEpisodeWatched> Episodes { get; set; }
}
