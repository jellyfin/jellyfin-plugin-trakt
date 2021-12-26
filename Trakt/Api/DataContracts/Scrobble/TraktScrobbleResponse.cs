using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Scrobble;

public class TraktScrobbleResponse
{
    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    [JsonPropertyName("sharing")]
    public SocialMedia Sharing { get; set; }

    [JsonPropertyName("movie")]
    public TraktMovie Movie { get; set; }

    [JsonPropertyName("episode")]
    public TraktEpisode Episode { get; set; }

    [JsonPropertyName("show")]
    public TraktShow Show { get; set; }
}
