using System.Text.Json.Serialization;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings;

/// <summary>
/// The trakt.tv users season rated class.
/// </summary>
public class TraktSeasonRated : TraktRated
{
    /// <summary>
    /// Gets or sets the season.
    /// </summary>
    [JsonPropertyName("season")]
    public TraktSeason Season { get; set; }
}
