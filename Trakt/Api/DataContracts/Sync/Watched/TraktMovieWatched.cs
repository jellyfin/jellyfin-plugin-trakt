using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Watched;

public class TraktMovieWatched : TraktMovie
{
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }
}
