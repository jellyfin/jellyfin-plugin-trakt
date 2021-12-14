using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Helpers;

namespace Trakt;

/// <summary>
/// All communication between the server and the plugins server instance should occur in this class.
/// </summary>
public class ServerMediator : IServerEntryPoint
{
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ServerMediator> _logger;
    private readonly UserDataManagerEventsHelper _userDataManagerEventsHelper;
    private readonly IUserDataManager _userDataManager;
    private TraktApi _traktApi;
    private LibraryManagerEventsHelper _libraryManagerEventsHelper;

    /// <summary>
    ///
    /// </summary>
    /// <param name="sessionManager"> </param>
    /// <param name="userDataManager"></param>
    /// <param name="libraryManager"> </param>
    /// <param name="logger"></param>
    /// <param name="httpClient"></param>
    /// <param name="appHost"></param>
    /// <param name="fileSystem"></param>
    public ServerMediator(
        ISessionManager sessionManager,
        IUserDataManager userDataManager,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        IFileSystem fileSystem)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;

        _logger = loggerFactory.CreateLogger<ServerMediator>();

        _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, fileSystem);
        _libraryManagerEventsHelper = new LibraryManagerEventsHelper(loggerFactory.CreateLogger<LibraryManagerEventsHelper>(), _traktApi);
        _userDataManagerEventsHelper = new UserDataManagerEventsHelper(loggerFactory.CreateLogger<UserDataManagerEventsHelper>(), _traktApi);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnUserDataSaved(object sender, UserDataSaveEventArgs e)
    {
        // ignore change events for any reason other than manually toggling played.
        if (e.SaveReason != UserDataSaveReason.TogglePlayed)
        {
            return;
        }

        if (e.Item != null)
        {
            // determine if user has trakt credentials
            var traktUser = UserHelper.GetTraktUser(e.UserId);

            // Can't progress
            if (traktUser == null || !_traktApi.CanSync(e.Item, traktUser))
            {
                return;
            }

            if (!traktUser.PostSetWatched && !traktUser.PostSetUnwatched)
            {
                // User doesn't want to post any status changes at all.
                return;
            }

            // We have a user who wants to post updates and the item is in a trakt monitored location.
            _userDataManagerEventsHelper.ProcessUserDataSaveEventArgs(e, traktUser);
        }
    }

    /// <inheritdoc />
    public Task RunAsync()
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        _sessionManager.PlaybackStart += KernelPlaybackStart;
        _sessionManager.PlaybackStopped += KernelPlaybackStopped;
        _libraryManager.ItemAdded += LibraryManagerItemAdded;
        _libraryManager.ItemRemoved += LibraryManagerItemRemoved;
        return Task.CompletedTask;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LibraryManagerItemRemoved(object sender, ItemChangeEventArgs e)
    {
        if (!(e.Item is Movie) && !(e.Item is Episode) && !(e.Item is Series))
        {
            return;
        }

        if (e.Item.LocationType == LocationType.Virtual)
        {
            return;
        }

        _libraryManagerEventsHelper.QueueItem(e.Item, EventType.Remove);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LibraryManagerItemAdded(object sender, ItemChangeEventArgs e)
    {
        // Don't do anything if it's not a supported media type
        if (!(e.Item is Movie) && !(e.Item is Episode) && !(e.Item is Series))
        {
            return;
        }

        if (e.Item.LocationType == LocationType.Virtual)
        {
            return;
        }

        _libraryManagerEventsHelper.QueueItem(e.Item, EventType.Add);
    }

    /// <summary>
    /// Let Trakt.tv know the user has started to watch something
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void KernelPlaybackStart(object sender, PlaybackProgressEventArgs e)
    {
        try
        {
            _logger.LogInformation("Playback Started");

            if (e.Users == null || !e.Users.Any() || e.Item == null)
            {
                _logger.LogError("Event details incomplete. Cannot process current media");
                return;
            }

            foreach (var user in e.Users)
            {
                // Since Emby is user profile friendly, I'm going to need to do a user lookup every time something starts
                var traktUser = UserHelper.GetTraktUser(user);

                if (traktUser == null)
                {
                    _logger.LogInformation("Could not match user with any stored credentials");
                    continue;
                }

                if (!traktUser.Scrobble)
                {
                    continue;
                }

                if (!_traktApi.CanSync(e.Item, traktUser))
                {
                    continue;
                }

                _logger.LogDebug("{UseId} appears to be monitoring {Path}", traktUser.LinkedMbUserId, e.Item.Path);

                var video = e.Item as Video;
                var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0 ?
                    (float)(e.PlaybackPositionTicks ?? 0) / video.RunTimeTicks.Value * 100.0f : 0.0f;

                try
                {
                    if (video is Movie movie)
                    {
                        _logger.LogDebug("Send movie status update");
                        await _traktApi.SendMovieStatusUpdateAsync(
                            movie,
                            MediaStatus.Watching,
                            traktUser,
                            progressPercent).ConfigureAwait(false);
                    }
                    else if (video is Episode episode)
                    {
                        _logger.LogDebug("Send episode status update");
                        await _traktApi.SendEpisodeStatusUpdateAsync(
                            episode,
                            MediaStatus.Watching,
                            traktUser,
                            progressPercent).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception handled sending status update");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending watching status update");
        }
    }

    /// <summary>
    /// Media playback has stopped. Depending on playback progress, let Trakt.tv know the user has
    /// completed watching the item.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void KernelPlaybackStopped(object sender, PlaybackStopEventArgs e)
    {
        if (e.Users == null || !e.Users.Any() || e.Item == null)
        {
            _logger.LogError("Event details incomplete. Cannot process current media");
            return;
        }

        try
        {
            _logger.LogInformation("Playback Stopped");

            foreach (var user in e.Users)
            {
                var traktUser = UserHelper.GetTraktUser(user);

                if (traktUser == null)
                {
                    _logger.LogError("Could not match trakt user");
                    continue;
                }

                if (!traktUser.Scrobble)
                {
                    continue;
                }

                if (!_traktApi.CanSync(e.Item, traktUser))
                {
                    continue;
                }

                var video = e.Item as Video;

                if (e.PlayedToCompletion)
                {
                    _logger.LogInformation("Item is played. Scrobble");

                    try
                    {
                        if (video is Movie movie)
                        {
                            await _traktApi.SendMovieStatusUpdateAsync(
                                movie,
                                MediaStatus.Stop,
                                traktUser,
                                100).ConfigureAwait(false);
                        }
                        else if (video is Episode episode)
                        {
                            await _traktApi.SendEpisodeStatusUpdateAsync(
                                episode,
                                MediaStatus.Stop,
                                traktUser,
                                100).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception handled sending status update");
                    }
                }
                else
                {
                    var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0 ?
                        (float)(e.PlaybackPositionTicks ?? 0) / video.RunTimeTicks.Value * 100.0f : 0.0f;
                    _logger.LogInformation("Item Not fully played. Tell trakt.tv we are no longer watching but don't scrobble");

                    if (video is Movie movie)
                    {
                        await _traktApi.SendMovieStatusUpdateAsync(movie, MediaStatus.Stop, traktUser, progressPercent).ConfigureAwait(false);
                    }
                    else
                    {
                        await _traktApi.SendEpisodeStatusUpdateAsync(video as Episode, MediaStatus.Stop, traktUser, progressPercent).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending scrobble");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _userDataManager.UserDataSaved -= OnUserDataSaved;
            _sessionManager.PlaybackStart -= KernelPlaybackStart;
            _sessionManager.PlaybackStopped -= KernelPlaybackStopped;
            _libraryManager.ItemAdded -= LibraryManagerItemAdded;
            _libraryManager.ItemRemoved -= LibraryManagerItemRemoved;
            _traktApi = null;
            _libraryManagerEventsHelper.Dispose();
            _libraryManagerEventsHelper = null;
            _userDataManagerEventsHelper.Dispose();
        }
    }
}
