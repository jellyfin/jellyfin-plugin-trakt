using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts.Users;

/// <summary>
/// Partial Trakt API response for GET /users/settings.
/// </summary>
public class TraktAccountSettings
{
    /// <summary>
    /// Gets or sets the user profile.
    /// </summary>
    [JsonPropertyName("user")]
    public TraktUserProfile User { get; set; }
}
