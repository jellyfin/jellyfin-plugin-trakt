using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.Sync;
using Trakt.Helpers;

namespace Trakt.Api;

/// <summary>
/// The Trakt.tv controller.
/// </summary>
[ApiController]
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
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="httpClient">Instance of the <see cref="IHttpClient"/> interface.</param>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    public TraktController(
        IUserDataManager userDataManager,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        IFileSystem fileSystem,
        ILibraryManager libraryManager)
    {
        _logger = loggerFactory.CreateLogger<TraktController>();
        _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, fileSystem);
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Authorize this server with trakt.
    /// </summary>
    /// <param name="userId">The user id of the user connecting to trakt.</param>
    /// <response code="200">Authorization code requested successfully.</response>
    /// <returns>The trakt authorization code.</returns>
    [HttpPost("Users/{userId}/Authorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> TraktDeviceAuthorization([FromRoute] string userId)
    {
        _logger.LogInformation("TraktDeviceAuthorization request received");

        // Create a user if we don't have one yet - TODO there should be an endpoint for this that creates a default user
        var traktUser = UserHelper.GetTraktUser(userId);
        if (traktUser == null)
        {
            Plugin.Instance.PluginConfiguration.AddUser(userId);
            traktUser = UserHelper.GetTraktUser(userId);
            Plugin.Instance.SaveConfiguration();
        }

        string userCode = await _traktApi.AuthorizeDevice(traktUser).ConfigureAwait(false);

        return new
        {
            userCode
        };
    }

    /// <summary>
    /// Poll the trakt device authorization status
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <response code="200">Polling successful.</response>
    /// <returns>A value indicating whether the authorization code was connected to a trakt account.</returns>
    [HttpGet("Users/{userId}/PollAuthorizationStatus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> TraktPollAuthorizationStatus([FromRoute] string userId)
    {
        _logger.LogInformation("TraktPollAuthorizationStatus request received");
        var traktUser = UserHelper.GetTraktUser(userId);
        bool isAuthorized = traktUser.AccessToken != null && traktUser.RefreshToken != null;

        if (Plugin.Instance.PollingTasks.TryGetValue(userId, out var task))
        {
            isAuthorized = task.Result;
            Plugin.Instance.PollingTasks.Remove(userId);
        }

        return new
        {
            isAuthorized
        };
    }

    /// <summary>
    /// Rate an item.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="itemId">The item id.</param>
    /// <param name="rating">Rating between 1 - 10 (0 = unrate).</param>
    /// <response code="200">Item rated successfully.</response>
    /// <returns>A <see cref="TraktSyncResponse"/>.</returns>
    [HttpPost("Users/{userId}/Items/{itemId}/Rate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TraktSyncResponse>> TraktRateItem([FromRoute] string userId, [FromRoute] Guid itemId, [FromQuery] int rating)
    {
        _logger.LogInformation("RateItem request received");

        var currentItem = _libraryManager.GetItemById(itemId);

        if (currentItem == null)
        {
            _logger.LogInformation("currentItem is null");
            return null;
        }

        return await _traktApi.SendItemRating(currentItem, rating, UserHelper.GetTraktUser(userId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Get recommended trakt movies.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <response code="200">Recommended movies returned.</response>
    /// <returns>A <see cref="List{TraktMovie}"/> with recommended movies.</returns>
    [HttpPost("Users/{userId}/RecommendedMovies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TraktMovie>>> RecommendedTraktMovies([FromRoute] string userId)
    {
        return await _traktApi.SendMovieRecommendationsRequest(UserHelper.GetTraktUser(userId)).ConfigureAwait(false);
    }

    /// <summary>
    /// Get recommended trakt shows.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <response code="200">Recommended shows returned.</response>
    /// <returns>A <see cref="List{TraktShow}"/> with recommended movies.</returns>
    [HttpPost("Users/{userId}/RecommendedShows")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TraktShow>>> RecommendedTraktShows([FromRoute] string userId)
    {
        return await _traktApi.SendShowRecommendationsRequest(UserHelper.GetTraktUser(userId)).ConfigureAwait(false);
    }
}
