using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Scrobble;

public class TraktScrobbleMovie
{
    [JsonPropertyName("movie")]
    public TraktMovie Movie { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; }

    [JsonPropertyName("app_date")]
    public string AppDate { get; set; }
}
