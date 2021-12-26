using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Scrobble;

public class TraktScrobbleEpisode
{
    [JsonPropertyName("show")]
    public TraktShow Show { get; set; }

    [JsonPropertyName("episode")]
    public TraktEpisode Episode { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; }

    [JsonPropertyName("app_date")]
    public string AppDate { get; set; }
}
