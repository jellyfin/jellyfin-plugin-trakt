using System;

namespace Trakt.Api.DataContracts.SelfService;

/// <summary>
/// Access token export payload for GET /Trakt/me/Token.
/// </summary>
public class TraktUserTokenDto
{
    /// <summary>
    /// Gets or sets the Trakt access token.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the access token expiration.
    /// </summary>
    public DateTime AccessTokenExpiration { get; set; }
}
