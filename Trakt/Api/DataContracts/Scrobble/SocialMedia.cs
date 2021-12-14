using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Scrobble;

public class SocialMedia
{
    [JsonPropertyName("facebook")]
    public bool Facebook { get; set; }

    [JsonPropertyName("twitter")]
    public bool Twitter { get; set; }

    [JsonPropertyName("tumblr")]
    public bool Tumblr { get; set; }
}
