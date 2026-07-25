using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users;

/// <summary>
/// User object from Trakt GET /users/settings.
/// </summary>
public class TraktUserProfile
{
    /// <summary>
    /// Gets or sets the trakt.tv username.
    /// </summary>
    [JsonPropertyName("username")]
    public string UserName { get; set; }
}
