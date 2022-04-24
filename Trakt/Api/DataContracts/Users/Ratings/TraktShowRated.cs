using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings;

/// <summary>
/// The trakt.tv users show rated class.
/// </summary>
public class TraktShowRated : TraktRated
{
    /// <summary>
    /// Gets or sets the show.
    /// </summary>
    [JsonPropertyName("show")]
    public TraktShow Show { get; set; }
}
