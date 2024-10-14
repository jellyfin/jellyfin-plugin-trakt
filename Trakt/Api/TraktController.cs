using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.Sync;
using Trakt.Helpers;

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
    /// Authorize this server with trakt.tv.
    /// </summary>
    /// <param name="userGuid">The GUID of the user connecting to trakt.tv.</param>
    /// <response code="200">Authorization code requested successfully.</response>
    /// <returns>The trakt.tv authorization code.</returns>
    [HttpPost("Users/{userGuid}/Authorize")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TraktDeviceAuthorization([FromRoute] Guid userGuid)
    {
        _logger.LogInformation("TraktDeviceAuthorization request received");

        // Create a user if we don't have one yet - TODO there should be an endpoint for this that creates a default user
        var traktUser = UserHelper.GetTraktUser(userGuid);
        if (traktUser == null)
        {
            _logger.LogWarning("No associated trakt.tv user found - creating one.");
            Plugin.Instance.PluginConfiguration.AddUser(userGuid);
            traktUser = UserHelper.GetTraktUser(userGuid);
            Plugin.Instance.SaveConfiguration();
        }

        string userCode = await _traktApi.AuthorizeDevice(traktUser).ConfigureAwait(false);

        return new
        {
            userCode
        };
    }

    /// <summary>
    /// Deauthorize this server with trakt.tv.
    /// </summary>
    /// <param name="userGuid">The GUID of the user connecting to trakt.tv.</param>
    /// <response code="200">Deauthorization successful.</response>
    /// <returns>Empty string.</returns>
    [HttpPost("Users/{userGuid}/Deauthorize")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public string TraktDeviceDeAuthorization([FromRoute] Guid userGuid)
    {
        _logger.LogInformation("TraktDeviceDeauthorization request received");

        // Delete a user
        var traktUser = UserHelper.GetTraktUser(userGuid);
        if (traktUser == null)
        {
            _logger.LogDebug("{User} not found.", userGuid);
        }
        else
        {
            _traktApi.DeauthorizeDevice(traktUser);
            Plugin.Instance.PluginConfiguration.RemoveUser(userGuid);
            Plugin.Instance.SaveConfiguration();
        }

        return string.Empty;
    }

    /// <summary>
    /// Poll the trakt.tv device authorization status.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <response code="200">Polling successful.</response>
    /// <returns>A value indicating whether the authorization code was connected to a trakt.tv account.</returns>
    [HttpGet("Users/{userGuid}/PollAuthorizationStatus")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> TraktPollAuthorizationStatus([FromRoute] Guid userGuid)
    {
        _logger.LogInformation("TraktPollAuthorizationStatus request received");
        var traktUser = UserHelper.GetTraktUser(userGuid);
        bool isAuthorized = traktUser.AccessToken != null && traktUser.RefreshToken != null;

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

    /// <summary>
    /// Rate an item.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <param name="itemId">The item id.</param>
    /// <param name="rating">Rating between 1 - 10 (0 = unrate).</param>
    /// <response code="200">Item rated successfully.</response>
    /// <returns>A <see cref="TraktSyncResponse"/>.</returns>
    [HttpPost("Users/{userGuid}/Items/{itemId}/Rate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TraktSyncResponse>> TraktRateItem([FromRoute] Guid userGuid, [FromRoute] Guid itemId, [FromQuery] int rating)
    {
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
    /// <returns>A <see cref="List{TraktMovie}"/> with recommended movies.</returns>
    [HttpPost("Users/{userGuid}/RecommendedMovies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TraktMovie>>> RecommendedTraktMovies([FromRoute] Guid userGuid)
    {
        return await _traktApi.SendMovieRecommendationsRequest(UserHelper.GetTraktUser(userGuid, true)).ConfigureAwait(false);
    }

    /// <summary>
    /// Get recommended trakt.tv shows.
    /// </summary>
    /// <param name="userGuid">The user's GUID.</param>
    /// <response code="200">Recommended shows returned.</response>
    /// <returns>A <see cref="List{TraktShow}"/> with recommended movies.</returns>
    [HttpPost("Users/{userGuid}/RecommendedShows")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TraktShow>>> RecommendedTraktShows([FromRoute] Guid userGuid)
    {
        return await _traktApi.SendShowRecommendationsRequest(UserHelper.GetTraktUser(userGuid, true)).ConfigureAwait(false);
    }
}
