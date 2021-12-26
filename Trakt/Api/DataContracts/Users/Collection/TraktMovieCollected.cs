using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Collection;

public class TraktMovieCollected
{
    [JsonPropertyName("collected_at")]
    public string CollectedAt { get; set; }

    [JsonPropertyName("metadata")]
    public TraktMetadata Metadata { get; set; }

    [JsonPropertyName("movie")]
    public TraktMovie Movie { get; set; }
}
