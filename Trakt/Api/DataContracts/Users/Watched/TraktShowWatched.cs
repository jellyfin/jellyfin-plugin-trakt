#pragma warning disable CA2227
#pragma warning disable CA1002

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Watched;

public class TraktShowWatched
{
    [JsonPropertyName("plays")]
    public int Plays { get; set; }

    [JsonPropertyName("reset_at")]
    public string ResetAt { get; set; }

    [JsonPropertyName("last_watched_at")]
    public string LastWatchedAt { get; set; }

    [JsonPropertyName("show")]
    public TraktShow Show { get; set; }

    [JsonPropertyName("seasons")]
    public List<Season> Seasons { get; set; }
}
