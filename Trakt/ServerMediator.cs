using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Helpers;
using Trakt.Model;
using Trakt.Model.Enums;

namespace Trakt;

/// <summary>
/// All communication between the server and the plugins server instance should occur in this class.
/// </summary>
public class ServerMediator : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ServerMediator> _logger;
    private readonly ISessionManager _sessionManager;
    private readonly IUserDataManager _userDataManager;
    private readonly UserDataManagerEventsHelper _userDataManagerEventsHelper;
    private readonly LibraryManagerEventsHelper _libraryManagerEventsHelper;
    private readonly TraktApi _traktApi;
    private readonly Dictionary<Guid, PlaybackState> _playbackState;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerMediator"/> class.
    /// </summary>
    /// <param name="sessionManager">The <see cref="ISessionManager"/>.</param>
    /// <param name="userDataManager">The <see cref="IUserDataManager"/>.</param>
    /// <param name="libraryManager">The <see cref="ILibraryManager"/>.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
    /// <param name="appHost">The <see cref="IServerApplicationHost"/>.</param>
    /// <param name="userManager">The <see cref="IUserManager"/>.</param>
    public ServerMediator(
        ISessionManager sessionManager,
        IUserDataManager userDataManager,
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        IUserManager userManager)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;

        _logger = loggerFactory.CreateLogger<ServerMediator>();
        _playbackState = new Dictionary<Guid, PlaybackState>();

        _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, userManager);
        _libraryManagerEventsHelper = new LibraryManagerEventsHelper(loggerFactory.CreateLogger<LibraryManagerEventsHelper>(), _traktApi);
        _userDataManagerEventsHelper = new UserDataManagerEventsHelper(loggerFactory.CreateLogger<UserDataManagerEventsHelper>(), _traktApi);
    }

    /// <summary>
    /// User data was saved.
    /// </summary>
    /// <param name="sender">The sending entity.</param>
    /// <param name="userDataSaveEventArgs">The <see cref="UserDataSaveEventArgs"/>.</param>
    private void OnUserDataSaved(object sender, UserDataSaveEventArgs userDataSaveEventArgs)
    {
        // Ignore change events for any reason other than manually toggling played.
        if (userDataSaveEventArgs.SaveReason != UserDataSaveReason.TogglePlayed)
        {
            return;
        }

        if (userDataSaveEventArgs.Item != null)
        {
            // Determine if user has trakt.tv credentials
            var traktUser = UserHelper.GetTraktUser(userDataSaveEventArgs.UserId, true);

            // Can't progress if user has no trakt.tv credentials
            if (traktUser == null || !_traktApi.CanSync(userDataSaveEventArgs.Item, traktUser))
            {
                return;
            }

            if (!traktUser.PostSetWatched && !traktUser.PostSetUnwatched)
            {
                // User doesn't want to post any status changes at all
                return;
            }

            // We have a user who wants to post updates and the item is in a trakt.tv monitored location
            _userDataManagerEventsHelper.ProcessUserDataSaveEventArgs(userDataSaveEventArgs, traktUser);
        }
    }

    /// <summary>
    /// Library item was removed.
    /// Let trakt.tv know which item was removed from the user's library.
    /// </summary>
    /// <param name="sender">The sending entity.</param>
    /// <param name="itemChangeEventArgs">The <see cref="ItemChangeEventArgs"/>.</param>
    private void LibraryManagerItemRemoved(object sender, ItemChangeEventArgs itemChangeEventArgs)
    {
        if (itemChangeEventArgs.Item is not Movie and not Episode and not Series)
        {
            return;
        }

        if (itemChangeEventArgs.Item.LocationType == LocationType.Virtual)
        {
            return;
        }

        _libraryManagerEventsHelper.QueueItem(itemChangeEventArgs.Item, EventType.Remove);
    }

    /// <summary>
    /// Library item was added.
    /// Let trakt.tv know which item was added to the user's library.
    /// </summary>
    /// <param name="sender">The sending entity.</param>
    /// <param name="itemChangeEventArgs">The <see cref="ItemChangeEventArgs"/>.</param>
    private void LibraryManagerItemAdded(object sender, ItemChangeEventArgs itemChangeEventArgs)
    {
        // Don't do anything if it's not a supported media type
        if (itemChangeEventArgs.Item is not Movie and not Episode and not Series)
        {
            return;
        }

        if (itemChangeEventArgs.Item.LocationType == LocationType.Virtual)
        {
            return;
        }

        _libraryManagerEventsHelper.QueueItem(itemChangeEventArgs.Item, EventType.Add);
    }

    /// <summary>
    /// Library item was updated.
    /// Let trakt.tv know which item was updated in the user's library.
    /// </summary>
    /// <param name="sender">The sending entity.</param>
    /// <param name="itemChangeEventArgs">The <see cref="ItemChangeEventArgs"/>.</param>
    private void LibraryManagerItemUpdated(object sender, ItemChangeEventArgs itemChangeEventArgs)
    {
        // Don't do anything if it's not a supported media type
        if (itemChangeEventArgs.Item is not Movie and not Episode and not Series)
        {
            return;
        }

        if (itemChangeEventArgs.Item.LocationType == LocationType.Virtual)
        {
            return;
        }

        _libraryManagerEventsHelper.QueueItem(itemChangeEventArgs.Item, EventType.Update);
    }

    /// <summary>
    /// Media playback has startet.
    /// Let trakt.tv know that the user has started playback.
    /// </summary>
    /// <param name="sender">The sending entity.</param>
    /// <param name="playbackProgressEventArgs">The <see cref="PlaybackProgressEventArgs"/>.</param>
    private async void KernelPlaybackStart(object sender, PlaybackProgressEventArgs playbackProgressEventArgs)
    {
        _logger.LogDebug("Playback started");

        if (playbackProgressEventArgs.Users == null || playbackProgressEventArgs.Users.Count == 0 || playbackProgressEventArgs.Item == null)
        {
            _logger.LogError("Event details incomplete. Cannot process current media");
            return;
        }

        if (playbackProgressEventArgs.Item is not Movie && playbackProgressEventArgs.Item is not Episode)
        {
            _logger.LogDebug("Syncing playback of {Item} is not supported by trakt.tv.", playbackProgressEventArgs.Item.Path);
            return;
        }

        foreach (var user in playbackProgressEventArgs.Users)
        {
            var traktUser = UserHelper.GetTraktUser(user, true);

            if (traktUser == null)
            {
                _logger.LogDebug("Could not match user {User} with any stored trakt.tv credentials.", user.Username);
                continue;
            }

            if (!traktUser.Scrobble)
            {
                _logger.LogDebug("User {User} disabled scrobbling to trakt.", user.Username);
                continue;
            }

            if (!_traktApi.CanSync(playbackProgressEventArgs.Item, traktUser))
            {
                _logger.LogDebug("Syncing playback of {Item} is forbidden for user {User}.", playbackProgressEventArgs.Item.Path, user.Username);
                continue;
            }

            var video = playbackProgressEventArgs.Item as Video;
            var playbackPositionTicks = playbackProgressEventArgs.PlaybackPositionTicks ?? 0L;
            var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0
                                    ? (float)playbackPositionTicks / video.RunTimeTicks.Value * 100.0f
                                    : 0.0f;

            _logger.LogDebug("User {User} started watching item {Item}.", user.Username, playbackProgressEventArgs.Item.Path);

            try
            {
                _playbackState[traktUser.LinkedMbUserId] = new PlaybackState
                {
                    IsPaused = false,
                    PlaybackPositionTicks = playbackPositionTicks,
                    PlaybackTime = DateTime.UtcNow
                };

                _logger.LogDebug("Sending {VideoType} playback status (Watching) update to trakt.tv for user {User}.", video.GetType().Name, user.Username);

                switch (video)
                {
                    case Movie movie:
                        await _traktApi.SendMovieStatusUpdateAsync(
                            movie,
                            MediaStatus.Watching,
                            traktUser,
                            progressPercent).ConfigureAwait(false);
                        break;
                    case Episode episode:
                        await _traktApi.SendEpisodeStatusUpdateAsync(
                            episode,
                            MediaStatus.Watching,
                            traktUser,
                            progressPercent).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending a playback status update to trakt.tv for user {User}.", user.Username);
            }
        }
    }

    /// <summary>
    /// Media playback has progressed.
    /// Let trakt.tv know that the user has progressed in playback.
    /// </summary>
    /// <param name="sender">The sending entity.</param>
    /// <param name="playbackProgressEventArgs">The <see cref="PlaybackProgressEventArgs"/>.</param>
    private async void KernelPlaybackProgress(object sender, PlaybackProgressEventArgs playbackProgressEventArgs)
    {
        _logger.LogDebug("Playback progressed");

        if (playbackProgressEventArgs.Users == null || playbackProgressEventArgs.Users.Count == 0 || playbackProgressEventArgs.Item == null)
        {
            _logger.LogError("Event details incomplete. Cannot process current media");
            return;
        }

        if (playbackProgressEventArgs.Item is not Movie && playbackProgressEventArgs.Item is not Episode)
        {
            _logger.LogDebug("Syncing playback of {Item} is not supported by trakt.tv.", playbackProgressEventArgs.Item.Path);
            return;
        }

        foreach (var user in playbackProgressEventArgs.Users)
        {
            var traktUser = UserHelper.GetTraktUser(user, true);

            if (traktUser == null)
            {
                _logger.LogDebug("Could not match user {User} with any stored trakt.tv credentials.", user.Username);
                continue;
            }

            if (!traktUser.Scrobble)
            {
                _logger.LogDebug("User {User} disabled scrobbling to trakt.", user.Username);
                continue;
            }

            if (!_traktApi.CanSync(playbackProgressEventArgs.Item, traktUser))
            {
                _logger.LogDebug("Syncing playback of {Item} is forbidden for user {User}.", playbackProgressEventArgs.Item.Path, user.Username);
                continue;
            }

            if (!_playbackState.TryGetValue(traktUser.LinkedMbUserId, out var state))
            {
                state = new PlaybackState();
            }

            var video = playbackProgressEventArgs.Item as Video;
            var playbackPositionTicks = playbackProgressEventArgs.PlaybackPositionTicks ?? 0L;
            var realTimeDifferenceInSeconds = Math.Round((DateTime.UtcNow - state.PlaybackTime).TotalSeconds);
            var tickDifferenceInSeconds = Math.Round(TimeSpan.FromTicks(playbackPositionTicks - state.PlaybackPositionTicks).TotalSeconds);
            var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0
                                    ? (float)playbackPositionTicks / video.RunTimeTicks.Value * 100.0f
                                    : 0.0f;

            try
            {
                if (!_playbackState.TryGetValue(traktUser.LinkedMbUserId, out state))
                {
                    _logger.LogWarning("Received playback progress from user {User} but initial state was never set - setting it now!", user.Username);
                    _playbackState[traktUser.LinkedMbUserId] = new PlaybackState
                    {
                        IsPaused = false,
                        PlaybackPositionTicks = playbackPositionTicks,
                        PlaybackTime = DateTime.UtcNow
                    };
                    continue;
                }

                state.PlaybackPositionTicks = playbackPositionTicks;
                state.PlaybackTime = DateTime.UtcNow;

                // Mark as skipped if tick difference is less than -10 seconds
                // or tick difference is ahead of real time difference by more than 10 seconds.
                var playbackSkipped = tickDifferenceInSeconds < -10 || tickDifferenceInSeconds > realTimeDifferenceInSeconds + 10;
                if (playbackProgressEventArgs.IsPaused == state.IsPaused && !playbackSkipped)
                {
                    _logger.LogDebug("Playback state did not change.");
                    continue;
                }

                if (playbackSkipped)
                {
                    _logger.LogDebug("Playback skipped.");
                }

                state.IsPaused = playbackProgressEventArgs.IsPaused;
                var status = state.IsPaused ? MediaStatus.Paused : MediaStatus.Watching;
                _logger.LogDebug("Sending {VideoType} playback status ({PlaybackStatus}) update to trakt.tv for user {User}.", video.GetType().Name, status, user.Username);

                switch (video)
                {
                    case Movie movie:
                        await _traktApi.SendMovieStatusUpdateAsync(
                            movie,
                            status,
                            traktUser,
                            progressPercent).ConfigureAwait(false);
                        break;
                    case Episode episode:
                        await _traktApi.SendEpisodeStatusUpdateAsync(
                            episode,
                            status,
                            traktUser,
                            progressPercent).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending a playback status update to trakt.tv for user {User}.", user.Username);
            }
        }
    }

    /// <summary>
    /// Media playback has stopped.
    /// Depending on playback progress, let trakt.tv know the user has completed watching the item.
    /// </summary>
    /// <param name="sender">The sending entity.</param>
    /// <param name="playbackStoppedEventArgs">The <see cref="PlaybackStopEventArgs"/>.</param>
    private async void KernelPlaybackStopped(object sender, PlaybackStopEventArgs playbackStoppedEventArgs)
    {
        if (playbackStoppedEventArgs.Users == null || playbackStoppedEventArgs.Users.Count == 0 || playbackStoppedEventArgs.Item == null)
        {
            _logger.LogError("Event details incomplete. Cannot process current media");
            return;
        }

        if (playbackStoppedEventArgs.Item is not Movie && playbackStoppedEventArgs.Item is not Episode)
        {
            _logger.LogDebug("Syncing playback of {Item} is not supported by trakt.tv.", playbackStoppedEventArgs.Item.Path);
            return;
        }

        _logger.LogDebug("Playback stopped");

        foreach (var user in playbackStoppedEventArgs.Users)
        {
            var traktUser = UserHelper.GetTraktUser(user, true);

            if (traktUser == null)
            {
                _logger.LogDebug("Could not match user {User} with any stored trakt.tv credentials.", user.Username);
                continue;
            }

            if (!traktUser.Scrobble)
            {
                _logger.LogDebug("User {User} disabled scrobbling to trakt.", user.Username);
                continue;
            }

            if (!_traktApi.CanSync(playbackStoppedEventArgs.Item, traktUser))
            {
                _logger.LogDebug("Syncing playback of {Item} is forbidden for user {User}.", playbackStoppedEventArgs.Item.Name, user.Username);
                continue;
            }

            var video = playbackStoppedEventArgs.Item as Video;

            try
            {
                if (playbackStoppedEventArgs.PlayedToCompletion)
                {
                    _logger.LogDebug("User {User} completed watching item {Item}. Scrobbling.", user.Username, playbackStoppedEventArgs.Item.Name);
                    _logger.LogDebug("Sending {VideoType} playback status (Stop) update to trakt.tv for user {User}.", video.GetType().Name, user.Username);

                    switch (video)
                    {
                        case Movie movie:
                            await _traktApi.SendMovieStatusUpdateAsync(
                                movie,
                                MediaStatus.Stop,
                                traktUser,
                                100).ConfigureAwait(false);
                            break;
                        case Episode episode:
                            await _traktApi.SendEpisodeStatusUpdateAsync(
                                episode,
                                MediaStatus.Stop,
                                traktUser,
                                100).ConfigureAwait(false);
                            break;
                    }
                }
                else
                {
                    var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0 ?
                        (float)(playbackStoppedEventArgs.PlaybackPositionTicks ?? 0) / video.RunTimeTicks.Value * 100.0f : 0.0f;

                    _logger.LogDebug("User {User} didn't watch item {Item} until the end. Not scrobbling but pausing playback at current playback position.", user.Username, playbackStoppedEventArgs.Item.Name);

                    switch (video)
                    {
                        case Movie movie:
                            await _traktApi.SendMovieStatusUpdateAsync(
                                movie,
                                MediaStatus.Paused,
                                traktUser,
                                progressPercent).ConfigureAwait(false);
                            break;
                        case Episode episode:
                            await _traktApi.SendEpisodeStatusUpdateAsync(
                                episode,
                                MediaStatus.Paused,
                                traktUser,
                                progressPercent).ConfigureAwait(false);
                            break;
                    }
                }

                _playbackState.Remove(traktUser.LinkedMbUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending a playback status update to trakt.tv for user {User}.", user.Username);
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        _sessionManager.PlaybackStart += KernelPlaybackStart;
        _sessionManager.PlaybackProgress += KernelPlaybackProgress;
        _sessionManager.PlaybackStopped += KernelPlaybackStopped;
        _libraryManager.ItemAdded += LibraryManagerItemAdded;
        _libraryManager.ItemUpdated += LibraryManagerItemUpdated;
        _libraryManager.ItemRemoved += LibraryManagerItemRemoved;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        _sessionManager.PlaybackStart -= KernelPlaybackStart;
        _sessionManager.PlaybackStopped -= KernelPlaybackStopped;
        _sessionManager.PlaybackProgress -= KernelPlaybackProgress;
        _libraryManager.ItemAdded -= LibraryManagerItemAdded;
        _libraryManager.ItemUpdated -= LibraryManagerItemUpdated;
        _libraryManager.ItemRemoved -= LibraryManagerItemRemoved;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    /// <param name="disposing">Whether to dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _userDataManagerEventsHelper?.Dispose();
            _libraryManagerEventsHelper?.Dispose();
        }
    }
}
