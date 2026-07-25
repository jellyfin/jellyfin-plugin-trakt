using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.SelfService;
using Trakt.Api.DataContracts.Sync;
using Trakt.Helpers;
using Trakt.Model;

namespace Trakt.Api;

/// <summary>
/// The trakt.tv controller class.
/// </summary>
[ApiController]
[Authorize]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class TraktController : ControllerBase
{
    private readonly TraktApi _traktApi;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<TraktController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktController"/> class.
    /// </summary>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    public TraktController(
        IUserDataManager userDataManager,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        ILibraryManager libraryManager,
        IUserManager userManager)
    {
        _logger = loggerFactory.CreateLogger<TraktController>();
        _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, userManager);
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Serves the standalone user self-service HTML page.
    /// Anonymous so the browser can load the document; the page authenticates API calls
    /// with the Jellyfin session from localStorage (same pattern as static /web pages).
    /// jellyfin-web's #/configurationpage route is admin-only and cannot host this UI.
    /// </summary>
    /// <response code="200">HTML page returned.</response>
    /// <response code="404">Embedded resource missing.</response>
    /// <returns>Self-service HTML.</returns>
    [HttpGet("SelfService")]
    [AllowAnonymous]
    [Produces(MediaTypeNames.Text.Html)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetSelfServicePage()
    {
        const string resourceName = "Trakt.Web.selfservice.html";
        var stream = typeof(Plugin).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return NotFound();
        }

        using (stream)
        using (var reader = new StreamReader(stream))
        {
            return Content(reader.ReadToEnd(), MediaTypeNames.Text.Html);
        }
    }

    /// <summary>
    /// Gets trakt.tv status and preferences for the current Jellyfin user.
    /// </summary>
    /// <response code="200">Status returned successfully.</response>
    /// <response code="401">Caller is not authenticated as a Jellyfin user.</response>
    /// <returns>The current user's Trakt status.</returns>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TraktUserStatusDto>> GetCurrentUserStatus()
    {
        if (!TryGetCallerUserId(out var userGuid))
        {
            return Unauthorized();
        }

        var traktUser = UserHelper.GetTraktUser(userGuid);
        if (traktUser == null)
        {
            // Do not create a config on read — that would inherit DefaultAllowExternalTokenAccess.
            return UnlinkedStatusDto(userGuid);
        }

        await EnsureTraktUserName(traktUser).ConfigureAwait(false);
        return ToStatusDto(traktUser);
    }

    /// <summary>
    /// Authorize the current Jellyfin user with trakt.tv.
    /// </summary>
    /// <response code="200">Authorization code requested successfully.</response>
    /// <response code="401">Caller is not authenticated as a Jellyfin user.</response>
    /// <returns>The trakt.tv device user code.</returns>
    [HttpPost("me/Authorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> AuthorizeCurrentUser()
    {
        if (!TryGetCallerUserId(out var userGuid))
        {
            return Unauthorized();
        }

        return await AuthorizeInternal(userGuid).ConfigureAwait(false);
    }

    /// <summary>
    /// Poll the trakt.tv device authorization status for the current Jellyfin user.
    /// </summary>
    /// <response code="200">Polling successful.</response>
    /// <response code="401">Caller is not authenticated as a Jellyfin user.</response>
    /// <returns>A value indicating whether authorization completed.</returns>
    [HttpGet("me/PollAuthorizationStatus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<object> PollCurrentUserAuthorizationStatus()
    {
        if (!TryGetCallerUserId(out var userGuid))
        {
            return Unauthorized();
        }

        return PollAuthorizationStatusInternal(userGuid);
    }

    /// <summary>
    /// Unlink the current Jellyfin user from trakt.tv while keeping preferences.
    /// </summary>
    /// <response code="200">Deauthorization successful.</response>
    /// <response code="401">Caller is not authenticated as a Jellyfin user.</response>
    /// <returns>Empty result.</returns>
    [HttpPost("me/Deauthorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult DeauthorizeCurrentUser()
    {
        if (!TryGetCallerUserId(out var userGuid))
        {
            return Unauthorized();
        }

        UnlinkTraktUser(userGuid);
        return Ok();
    }

    /// <summary>
    /// Updates trakt.tv preferences for the current Jellyfin user.
    /// </summary>
    /// <param name="settings">The preference update payload.</param>
    /// <response code="200">Settings saved successfully.</response>
    /// <response code="400">Settings payload was missing.</response>
    /// <response code="401">Caller is not authenticated as a Jellyfin user.</response>
    /// <returns>The updated status.</returns>
    [HttpPut("me/Settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TraktUserStatusDto> UpdateCurrentUserSettings([FromBody] TraktUserSettingsUpdateDto settings)
    {
        if (!TryGetCallerUserId(out var userGuid))
        {
            return Unauthorized();
        }

        if (settings == null)
        {
            return BadRequest();
        }

        var traktUser = EnsureTraktUser(userGuid);
        ApplySettings(traktUser, settings);
        Plugin.Instance.SaveConfiguration();
        return ToStatusDto(traktUser);
    }

    /// <summary>
    /// Gets the Trakt access token for the current user when an administrator enabled external token access.
    /// </summary>
    /// <response code="200">Token returned successfully.</response>
    /// <response code="401">Caller is not authenticated as a Jellyfin user.</response>
    /// <response code="403">Admin has not allowed token export, or the account is not linked.</response>
    /// <returns>The access token and expiration.</returns>
    [HttpGet("me/Token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TraktUserTokenDto>> GetCurrentUserToken()
    {
        if (!TryGetCallerUserId(out var userGuid))
        {
            return Unauthorized();
        }

        var traktUser = UserHelper.GetTraktUser(userGuid);
        if (traktUser == null
            || !traktUser.AllowExternalTokenAccess
            || string.IsNullOrWhiteSpace(traktUser.AccessToken)
            || string.IsNullOrWhiteSpace(traktUser.RefreshToken))
        {
            return Forbid();
        }

        if (DateTimeOffset.Now > traktUser.AccessTokenExpiration)
        {
            await _traktApi.RefreshUserAccessToken(traktUser).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(traktUser.AccessToken))
        {
            return Forbid();
        }

        return new TraktUserTokenDto
        {
            AccessToken = traktUser.AccessToken,
            AccessTokenExpiration = traktUser.AccessTokenExpiration
        };
    }

    /// <summary>
    /// Authorize this server with trakt.tv.
    /// </summary>
    /// <param name="userGuid">The GUID of the user connecting to trakt.tv.</param>
    /// <response code="200">Authorization code requested successfully.</response>
    /// <response code="403">Caller is not allowed to manage the specified user.</response>
    /// <returns>The trakt.tv authorization code.</returns>
    [HttpPost("Users/{userGuid}/Authorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> TraktDeviceAuthorization([FromRoute] Guid userGuid)
    {
        if (!AuthorizationHelper.CanAccessUser(User, userGuid))
        {
            return Forbid();
        }

        _logger.LogInformation("TraktDeviceAuthorization request received");
        return await AuthorizeInternal(userGuid).ConfigureAwait(false);
    }

    /// <summary>
    /// Unlink a user from trakt.tv while keeping preferences.
    /// </summary>
    /// <param name="userGuid">The GUID of the user connecting to trakt.tv.</param>
    /// <response code="200">Deauthorization successful.</response>
    /// <response code="403">Caller is not allowed to manage the specified user.</response>
    /// <returns>Empty string.</returns>
    [HttpPost("Users/{userGuid}/Deauthorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult TraktDeviceDeAuthorization([FromRoute] Guid userGuid)
    {
        if (!AuthorizationHelper.CanAccessUser(User, userGuid))
        {
            return Forbid();
        }

        _logger.LogInformation("TraktDeviceDeauthorization request received");
        UnlinkTraktUser(userGuid);
        return Ok(string.Empty);
    }

    /// <summary>
    /// Poll the trakt.tv device authorization status.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <response code="200">Polling successful.</response>
    /// <response code="403">Caller is not allowed to manage the specified user.</response>
    /// <returns>A value indicating whether the authorization code was connected to a trakt.tv account.</returns>
    [HttpGet("Users/{userGuid}/PollAuthorizationStatus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<object> TraktPollAuthorizationStatus([FromRoute] Guid userGuid)
    {
        if (!AuthorizationHelper.CanAccessUser(User, userGuid))
        {
            return Forbid();
        }

        _logger.LogInformation("TraktPollAuthorizationStatus request received");
        return PollAuthorizationStatusInternal(userGuid);
    }

    /// <summary>
    /// Refresh the stored trakt.tv username for a user.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <response code="200">Profile refreshed (or already present).</response>
    /// <response code="403">Caller is not allowed to manage the specified user.</response>
    /// <response code="404">No linked Trakt user found.</response>
    /// <returns>The trakt.tv username when available.</returns>
    [HttpPost("Users/{userGuid}/RefreshProfile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> TraktRefreshProfile([FromRoute] Guid userGuid)
    {
        if (!AuthorizationHelper.CanAccessUser(User, userGuid))
        {
            return Forbid();
        }

        var traktUser = UserHelper.GetTraktUser(userGuid);
        if (traktUser == null || string.IsNullOrWhiteSpace(traktUser.AccessToken))
        {
            return NotFound();
        }

        await _traktApi.RefreshUserProfile(traktUser).ConfigureAwait(false);
        return new
        {
            userName = traktUser.UserName
        };
    }

    /// <summary>
    /// Rate an item.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <param name="itemId">The item id.</param>
    /// <param name="rating">Rating between 1 - 10 (0 = unrate).</param>
    /// <response code="200">Item rated successfully.</response>
    /// <response code="403">Caller is not allowed to manage the specified user.</response>
    /// <returns>A <see cref="TraktSyncResponse"/>.</returns>
    [HttpPost("Users/{userGuid}/Items/{itemId}/Rate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TraktSyncResponse>> TraktRateItem([FromRoute] Guid userGuid, [FromRoute] Guid itemId, [FromQuery] int rating)
    {
        if (!AuthorizationHelper.CanAccessUser(User, userGuid))
        {
            return Forbid();
        }

        _logger.LogInformation("RateItem request received");

        var currentItem = _libraryManager.GetItemById(itemId);

        if (currentItem == null)
        {
            _logger.LogInformation("currentItem is null");
            return null;
        }

        return await _traktApi.SendItemRating(currentItem, rating, UserHelper.GetTraktUser(userGuid, true)).ConfigureAwait(false);
    }

    /// <summary>
    /// Get recommended trakt.tv movies.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <response code="200">Recommended movies returned.</response>
    /// <response code="403">Caller is not allowed to manage the specified user.</response>
    /// <returns>A <see cref="List{TraktMovie}"/> with recommended movies.</returns>
    [HttpPost("Users/{userGuid}/RecommendedMovies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<TraktMovie>>> RecommendedTraktMovies([FromRoute] Guid userGuid)
    {
        if (!AuthorizationHelper.CanAccessUser(User, userGuid))
        {
            return Forbid();
        }

        return await _traktApi.SendMovieRecommendationsRequest(UserHelper.GetTraktUser(userGuid, true)).ConfigureAwait(false);
    }

    /// <summary>
    /// Get recommended trakt.tv shows.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <response code="200">Recommended shows returned.</response>
    /// <response code="403">Caller is not allowed to manage the specified user.</response>
    /// <returns>A <see cref="List{TraktShow}"/> with recommended shows.</returns>
    [HttpPost("Users/{userGuid}/RecommendedShows")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<TraktShow>>> RecommendedTraktShows([FromRoute] Guid userGuid)
    {
        if (!AuthorizationHelper.CanAccessUser(User, userGuid))
        {
            return Forbid();
        }

        return await _traktApi.SendShowRecommendationsRequest(UserHelper.GetTraktUser(userGuid, true)).ConfigureAwait(false);
    }

    private bool TryGetCallerUserId(out Guid userGuid)
    {
        userGuid = AuthorizationHelper.GetAuthenticatedUserId(User);
        return !userGuid.Equals(Guid.Empty);
    }

    private async Task<ActionResult<object>> AuthorizeInternal(Guid userGuid)
    {
        var traktUser = EnsureTraktUser(userGuid);
        string userCode = await _traktApi.AuthorizeDevice(traktUser).ConfigureAwait(false);

        return new
        {
            userCode
        };
    }

    private ActionResult<object> PollAuthorizationStatusInternal(Guid userGuid)
    {
        var traktUser = UserHelper.GetTraktUser(userGuid);
        bool isAuthorized = traktUser != null
                            && !string.IsNullOrEmpty(traktUser.AccessToken)
                            && !string.IsNullOrEmpty(traktUser.RefreshToken);

        if (Plugin.Instance.PollingTasks.TryGetValue(userGuid, out var task))
        {
            isAuthorized = task.Result;
            Plugin.Instance.PollingTasks.Remove(userGuid);
        }

        return new
        {
            isAuthorized
        };
    }

    private TraktUser EnsureTraktUser(Guid userGuid)
    {
        var traktUser = UserHelper.GetTraktUser(userGuid);
        if (traktUser != null)
        {
            return traktUser;
        }

        _logger.LogWarning("No associated trakt.tv user found - creating one.");
        Plugin.Instance.PluginConfiguration.AddUser(userGuid);
        Plugin.Instance.SaveConfiguration();
        return UserHelper.GetTraktUser(userGuid);
    }

    private void UnlinkTraktUser(Guid userGuid)
    {
        var traktUser = UserHelper.GetTraktUser(userGuid);
        if (traktUser == null)
        {
            _logger.LogDebug("{User} not found.", userGuid);
            return;
        }

        _traktApi.DeauthorizeDevice(traktUser);
        traktUser.AccessToken = null;
        traktUser.RefreshToken = null;
        traktUser.UserName = null;
        traktUser.AccessTokenExpiration = DateTime.MinValue;
        Plugin.Instance.SaveConfiguration();
    }

    private async Task EnsureTraktUserName(TraktUser traktUser)
    {
        if (traktUser == null
            || string.IsNullOrWhiteSpace(traktUser.AccessToken)
            || string.IsNullOrWhiteSpace(traktUser.RefreshToken)
            || !string.IsNullOrWhiteSpace(traktUser.UserName))
        {
            return;
        }

        await _traktApi.RefreshUserProfile(traktUser).ConfigureAwait(false);
    }

    private static void ApplySettings(TraktUser traktUser, TraktUserSettingsUpdateDto settings)
    {
        traktUser.SkipUnwatchedImportFromTrakt = settings.SkipUnwatchedImportFromTrakt;
        traktUser.SkipPlaybackProgressImportFromTrakt = settings.SkipPlaybackProgressImportFromTrakt;
        traktUser.SkipWatchedImportFromTrakt = settings.SkipWatchedImportFromTrakt;
        traktUser.PostWatchedHistory = settings.PostWatchedHistory;
        traktUser.PostUnwatchedHistory = settings.PostUnwatchedHistory;
        traktUser.PostSetWatched = settings.PostSetWatched;
        traktUser.PostSetUnwatched = settings.PostSetUnwatched;
        traktUser.ExportMediaInfo = settings.ExportMediaInfo;
        traktUser.SynchronizeCollections = settings.SynchronizeCollections;
        traktUser.Scrobble = settings.Scrobble;
        traktUser.DontRemoveItemFromTrakt = settings.DontRemoveItemFromTrakt;
        // Admin-only via plugin configuration page:
        // ExtraLogging, LocationsExcluded, AllowExternalTokenAccess.
    }

    private static TraktUserStatusDto UnlinkedStatusDto(Guid userGuid)
    {
        // Mirror TraktUser constructor defaults for the self-service form before first save/authorize.
        return new TraktUserStatusDto
        {
            LinkedMbUserId = userGuid,
            IsLinked = false,
            UserName = null,
            AccessTokenExpiration = null,
            SkipUnwatchedImportFromTrakt = true,
            SkipPlaybackProgressImportFromTrakt = false,
            SkipWatchedImportFromTrakt = false,
            PostWatchedHistory = true,
            PostUnwatchedHistory = false,
            PostSetWatched = true,
            PostSetUnwatched = false,
            ExportMediaInfo = true,
            SynchronizeCollections = true,
            Scrobble = true,
            DontRemoveItemFromTrakt = true,
            AllowExternalTokenAccess = false
        };
    }

    private static TraktUserStatusDto ToStatusDto(TraktUser traktUser)
    {
        var isLinked = !string.IsNullOrWhiteSpace(traktUser.AccessToken)
                       && !string.IsNullOrWhiteSpace(traktUser.RefreshToken);

        return new TraktUserStatusDto
        {
            LinkedMbUserId = traktUser.LinkedMbUserId,
            IsLinked = isLinked,
            UserName = isLinked ? traktUser.UserName : null,
            AccessTokenExpiration = isLinked ? traktUser.AccessTokenExpiration : null,
            SkipUnwatchedImportFromTrakt = traktUser.SkipUnwatchedImportFromTrakt,
            SkipPlaybackProgressImportFromTrakt = traktUser.SkipPlaybackProgressImportFromTrakt,
            SkipWatchedImportFromTrakt = traktUser.SkipWatchedImportFromTrakt,
            PostWatchedHistory = traktUser.PostWatchedHistory,
            PostUnwatchedHistory = traktUser.PostUnwatchedHistory,
            PostSetWatched = traktUser.PostSetWatched,
            PostSetUnwatched = traktUser.PostSetUnwatched,
            ExportMediaInfo = traktUser.ExportMediaInfo,
            SynchronizeCollections = traktUser.SynchronizeCollections,
            Scrobble = traktUser.Scrobble,
            DontRemoveItemFromTrakt = traktUser.DontRemoveItemFromTrakt,
            AllowExternalTokenAccess = traktUser.AllowExternalTokenAccess
        };
    }
}
