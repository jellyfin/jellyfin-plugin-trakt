using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users.Watched;

public class Episode
{
    [JsonPropertyName("last_watched_at")]
    public string LastWatchedAt { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("plays")]
    public int Plays { get; set; }
}
