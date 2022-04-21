using System;
using System.Collections.Generic;
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
using Trakt.Model.Enums;

namespace Trakt
{
    /// <summary>
    /// All communication between the server and the plugins server instance should occur in this class.
    /// </summary>
    public class ServerMediator : IServerEntryPoint, IDisposable
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<ServerMediator> _logger;
        private readonly UserDataManagerEventsHelper _userDataManagerEventsHelper;
        private readonly IUserDataManager _userDataManager;
        private TraktApi _traktApi;
        private LibraryManagerEventsHelper _libraryManagerEventsHelper;

        private Dictionary<string, bool> _playbackPause;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerMediator"/> class.
        /// </summary>
        /// <param name="sessionManager">The <see cref="ISessionManager"/>.</param>
        /// <param name="userDataManager">The <see cref="IUserDataManager"/>.</param>
        /// <param name="libraryManager">The <see cref="ILibraryManager"/>.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
        /// <param name="appHost">The <see cref="IServerApplicationHost"/>.</param>
        /// <param name="fileSystem">The <see cref="IFileSystem"/>.</param>
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
            _playbackPause = new Dictionary<string, bool>();

            _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, fileSystem);
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
                var traktUser = UserHelper.GetTraktUser(userDataSaveEventArgs.UserId);

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
        /// Run observer tasks for observed events.
        /// </summary>
        /// <returns>Task.</returns>
        public Task RunAsync()
        {
            _userDataManager.UserDataSaved += OnUserDataSaved;
            _sessionManager.PlaybackStart += KernelPlaybackStart;
            _sessionManager.PlaybackProgress += KernelPlaybackProgress;
            _sessionManager.PlaybackStopped += KernelPlaybackStopped;
            _libraryManager.ItemAdded += LibraryManagerItemAdded;
            _libraryManager.ItemRemoved += LibraryManagerItemRemoved;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Library item was removed.
        /// Let trakt.tv know which item was removed from the user's library.
        /// </summary>
        /// <param name="sender">The sending entity.</param>
        /// <param name="itemChangeEventArgs">The <see cref="ItemChangeEventArgs"/>.</param>
        private void LibraryManagerItemRemoved(object sender, ItemChangeEventArgs itemChangeEventArgs)
        {
            if (!(itemChangeEventArgs.Item is Movie) && !(itemChangeEventArgs.Item is Episode) && !(itemChangeEventArgs.Item is Series))
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
            if (!(itemChangeEventArgs.Item is Movie) && !(itemChangeEventArgs.Item is Episode) && !(itemChangeEventArgs.Item is Series))
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
        /// Media playback has startet.
        /// Let trakt.tv know that the user has started playback.
        /// </summary>
        /// <param name="sender">The sending entity.</param>
        /// <param name="playbackProgressEventArgs">The <see cref="PlaybackProgressEventArgs"/>.</param>
        private async void KernelPlaybackStart(object sender, PlaybackProgressEventArgs playbackProgressEventArgs)
        {
            _logger.LogDebug("Playback started");

            if (playbackProgressEventArgs.Users == null || !playbackProgressEventArgs.Users.Any() || playbackProgressEventArgs.Item == null)
            {
                _logger.LogError("Event details incomplete. Cannot process current media");
                return;
            }

            foreach (var user in playbackProgressEventArgs.Users)
            {
                // Since Jellyfin supports user profiles we need to do a user lookup every time something starts
                var traktUser = UserHelper.GetTraktUser(user);

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
                    _logger.LogDebug("Syncing playback for {Item} is forbidden for user {User}.", playbackProgressEventArgs.Item.Path, user.Username);
                    continue;
                }

                var video = playbackProgressEventArgs.Item as Video;
                var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0 ?
                    (float)(playbackProgressEventArgs.PlaybackPositionTicks ?? 0) / video.RunTimeTicks.Value * 100.0f : 0.0f;

                _logger.LogDebug("User {User} started watching item {Item}.", user.Username, playbackProgressEventArgs.Item.Path);

                try
                {
                    if (video is Movie movie)
                    {
                        _logger.LogDebug("Sending movie playback status update to trakt.tv for user {User}.", user.Username);
                        await _traktApi.SendMovieStatusUpdateAsync(
                            movie,
                            MediaStatus.Watching,
                            traktUser,
                            progressPercent).ConfigureAwait(false);
                        _playbackPause[traktUser.LinkedMbUserId] = false;
                    }
                    else if (video is Episode episode)
                    {
                        _logger.LogDebug("Sending episode playback status update to trakt.tv for user {User}.", user.Username);
                        await _traktApi.SendEpisodeStatusUpdateAsync(
                            episode,
                            MediaStatus.Watching,
                            traktUser,
                            progressPercent).ConfigureAwait(false);
                        _playbackPause[traktUser.LinkedMbUserId] = false;
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

            if (playbackProgressEventArgs.Users == null || !playbackProgressEventArgs.Users.Any() || playbackProgressEventArgs.Item == null)
            {
                _logger.LogError("Event details incomplete. Cannot process current media");
                return;
            }

            foreach (var user in playbackProgressEventArgs.Users)
            {
                // Since Emby is user profile friendly, I'm going to need to do a user lookup every time something starts
                var traktUser = UserHelper.GetTraktUser(user);

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
                    _logger.LogDebug("Syncing playback for {Item} is forbidden for user {User}.", playbackProgressEventArgs.Item.Path, user.Username);
                    continue;
                }

                var video = playbackProgressEventArgs.Item as Video;
                var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0 ?
                    (float)(playbackProgressEventArgs.PlaybackPositionTicks ?? 0) / video.RunTimeTicks.Value * 100.0f : 0.0f;

                _logger.LogDebug("User {User} progressed watching item {Item}.", user.Username, playbackProgressEventArgs.Item.Path);

                try
                {
                    if (playbackProgressEventArgs.IsPaused)
                    {
                        _logger.LogDebug("Playback paused");

                        if (video is Movie movie)
                        {
                            _logger.LogDebug("Sending movie playback status update to trakt.tv for user {User}.", user.Username);
                            await _traktApi.SendMovieStatusUpdateAsync(
                                movie,
                                MediaStatus.Paused,
                                traktUser,
                                progressPercent).ConfigureAwait(false);
                            _playbackPause[traktUser.LinkedMbUserId] = true;
                        }
                        else if (video is Episode episode)
                        {
                            _logger.LogDebug("Sending episode playback status update to trakt.tv for user {User}.", user.Username);
                            await _traktApi.SendEpisodeStatusUpdateAsync(
                                episode,
                                MediaStatus.Paused,
                                traktUser,
                                progressPercent).ConfigureAwait(false);
                            _playbackPause[traktUser.LinkedMbUserId] = true;
                        }
                    }
                    else
                    {
                        _playbackPause.TryGetValue(traktUser.LinkedMbUserId, out bool paused);
                        if (paused)
                        {
                            _logger.LogDebug("Playback resumed");

                            if (video is Movie movie)
                            {
                                _logger.LogDebug("Sending movie playback status update to trakt.tv for user {User}.", user.Username);
                                await _traktApi.SendMovieStatusUpdateAsync(
                                    movie,
                                    MediaStatus.Watching,
                                    traktUser,
                                    progressPercent).ConfigureAwait(false);
                            }
                            else if (video is Episode episode)
                            {
                                _logger.LogDebug("Sending episode playback status update to trakt.tv for user {User}.", user.Username);
                                await _traktApi.SendEpisodeStatusUpdateAsync(
                                    episode,
                                    MediaStatus.Watching,
                                    traktUser,
                                    progressPercent).ConfigureAwait(false);
                            }

                            _playbackPause[traktUser.LinkedMbUserId] = false;
                        }
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
            if (playbackStoppedEventArgs.Users == null || !playbackStoppedEventArgs.Users.Any() || playbackStoppedEventArgs.Item == null)
            {
                _logger.LogError("Event details incomplete. Cannot process current media");
                return;
            }

            _logger.LogInformation("Playback stopped");

            foreach (var user in playbackStoppedEventArgs.Users)
            {
                var traktUser = UserHelper.GetTraktUser(user);

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
                    _logger.LogDebug("Syncing playback for {Item} is forbidden for user {User}.", playbackStoppedEventArgs.Item.Name, user.Username);
                    continue;
                }

                var video = playbackStoppedEventArgs.Item as Video;

                try
                {
                    if (playbackStoppedEventArgs.PlayedToCompletion)
                    {
                        _logger.LogDebug("User {User} completed watching item {Item}. Scrobbling.", user.Username, playbackStoppedEventArgs.Item.Name);

                        if (video is Movie movie)
                        {
                            _logger.LogDebug("Sending movie playback status update to trakt.tv for user {User}.", user.Username);
                            await _traktApi.SendMovieStatusUpdateAsync(
                                movie,
                                MediaStatus.Stop,
                                traktUser,
                                100).ConfigureAwait(false);
                        }
                        else if (video is Episode episode)
                        {
                            _logger.LogDebug("Sending episode playback status update to trakt.tv for user {User}.", user.Username);
                            await _traktApi.SendEpisodeStatusUpdateAsync(
                                episode,
                                MediaStatus.Stop,
                                traktUser,
                                100).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        var progressPercent = video.RunTimeTicks.HasValue && video.RunTimeTicks != 0 ?
                            (float)(playbackStoppedEventArgs.PlaybackPositionTicks ?? 0) / video.RunTimeTicks.Value * 100.0f : 0.0f;

                        _logger.LogDebug("User {User} didn't watch item {Item} until the end. Not scrobbling but pausing playback at current playback position.", user.Username, playbackStoppedEventArgs.Item.Name);

                        if (video is Movie movie)
                        {
                            await _traktApi.SendMovieStatusUpdateAsync(
                                movie,
                                MediaStatus.Paused,
                                traktUser,
                                progressPercent).ConfigureAwait(false);
                        }
                        else
                        {
                            await _traktApi.SendEpisodeStatusUpdateAsync(
                                video as Episode,
                                MediaStatus.Paused,
                                traktUser,
                                progressPercent).ConfigureAwait(false);
                        }
                    }

                    _playbackPause[traktUser.LinkedMbUserId] = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred while sending a playback status update to trakt.tv for user {User}.", user.Username);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Removes event subscriptions on dispose.
        /// </summary>
        /// <param name="disposing"><see cref="bool"/> indicating if object is currently disposed.</param>
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
}
