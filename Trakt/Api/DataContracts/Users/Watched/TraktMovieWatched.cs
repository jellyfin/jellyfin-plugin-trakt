using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Watched;

public class TraktMovieWatched
{
    [JsonPropertyName("plays")]
    public int Plays { get; set; }

    [JsonPropertyName("last_watched_at")]
    public string LastWatchedAt { get; set; }

    [JsonPropertyName("movie")]
    public TraktMovie Movie { get; set; }
}
