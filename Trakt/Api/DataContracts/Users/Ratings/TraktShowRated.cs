using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings;

public class TraktShowRated : TraktRated
{
    [JsonPropertyName("show")]
    public TraktShow Show { get; set; }
}
