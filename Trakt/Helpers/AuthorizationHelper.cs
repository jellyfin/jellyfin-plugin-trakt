using System;
using System.Linq;
using System.Security.Claims;

namespace Trakt.Helpers;

/// <summary>
/// Helpers for resolving the authenticated Jellyfin user and ownership checks.
/// </summary>
internal static class AuthorizationHelper
{
    private const string UserIdClaimType = "Jellyfin-UserId";
    private const string AdministratorRole = "Administrator";

    /// <summary>
    /// Gets the authenticated Jellyfin user id from claims.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <returns>The user GUID, or <see cref="Guid.Empty"/> if missing.</returns>
    public static Guid GetAuthenticatedUserId(ClaimsPrincipal user)
    {
        var value = user?.Claims.FirstOrDefault(claim =>
            claim.Type.Equals(UserIdClaimType, StringComparison.OrdinalIgnoreCase))?.Value;

        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Gets a value indicating whether the principal is an administrator.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <returns><c>true</c> if the user is an administrator; otherwise <c>false</c>.</returns>
    public static bool IsAdministrator(ClaimsPrincipal user)
    {
        return user != null && user.IsInRole(AdministratorRole);
    }

    /// <summary>
    /// Gets a value indicating whether the principal may act on the given Jellyfin user.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <param name="userGuid">The target Jellyfin user id.</param>
    /// <returns><c>true</c> if the caller is that user or an administrator.</returns>
    public static bool CanAccessUser(ClaimsPrincipal user, Guid userGuid)
    {
        var authenticatedUserId = GetAuthenticatedUserId(user);
        return !authenticatedUserId.Equals(Guid.Empty)
               && (authenticatedUserId.Equals(userGuid) || IsAdministrator(user));
    }
}
