using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Model;

namespace Trakt.Helpers;

/// <summary>
/// Helper class used to update the watched status of movies/episodes.
/// Attempts to organise requests to lower API calls.
/// </summary>
internal sealed class UserDataManagerEventsHelper : IDisposable
{
    private readonly ILogger<UserDataManagerEventsHelper> _logger;
    private readonly TraktApi _traktApi;
    private readonly Dictionary<Guid, UserDataPackage> _userDataPackages;
    private Timer _queueTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDataManagerEventsHelper"/> class.
    /// </summary>
    /// <param name="logger">The <see cref="ILogger{UserDataManagerEventsHelper}"/>.</param>
    /// <param name="traktApi">The <see cref="TraktApi"/>.</param>
    public UserDataManagerEventsHelper(ILogger<UserDataManagerEventsHelper> logger, TraktApi traktApi)
    {
        _userDataPackages = new Dictionary<Guid, UserDataPackage>();
        _logger = logger;
        _traktApi = traktApi;
    }

    /// <summary>
    /// Process user data save event for trakt.tv users.
    /// </summary>
    /// <param name="userDataSaveEventArgs">The <see cref="UserDataSaveEventArgs"/>.</param>
    /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
    public void ProcessUserDataSaveEventArgs(UserDataSaveEventArgs userDataSaveEventArgs, TraktUser traktUser)
    {
        lock (_userDataPackages)
        {
            _userDataPackages.TryGetValue(traktUser.LinkedMbUserId, out var userPackage);

            if (userPackage == null)
            {
                userPackage = new UserDataPackage();
            }

            if (_queueTimer == null)
            {
                _queueTimer = new Timer(
                    OnTimerCallback,
                    null,
                    TimeSpan.FromMilliseconds(5000),
                    Timeout.InfiniteTimeSpan);
            }
            else
            {
                _queueTimer.Change(TimeSpan.FromMilliseconds(5000), Timeout.InfiniteTimeSpan);
            }

            if (userDataSaveEventArgs.Item is Movie movie)
            {
                if (traktUser.PostSetWatched && userDataSaveEventArgs.UserData.Played)
                {
                    userPackage.SeenMovies.Add(movie);

                    // Force update trakt.tv if we have more than 100 seen movies in the queue due to API
                    if (userPackage.SeenMovies.Count >= 100)
                    {
                        _traktApi.SendMoviePlaystateUpdates(
                            userPackage.SeenMovies.ToList(),
                            traktUser,
                            true,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.SeenMovies.Clear();
                    }
                }
                else if (traktUser.PostSetUnwatched)
                {
                    userPackage.UnSeenMovies.Add(movie);

                    // Force update trakt.tv if we have more than 100 unseen movies in the queue due to API
                    if (userPackage.UnSeenMovies.Count >= 100)
                    {
                        _traktApi.SendMoviePlaystateUpdates(
                            userPackage.UnSeenMovies.ToList(),
                            traktUser,
                            false,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.UnSeenMovies.Clear();
                    }
                }
            }
            else if (userDataSaveEventArgs.Item is Episode episode)
            {
                // If it's not the series we're currently storing, upload our episodes and reset the arrays
                if (userPackage.CurrentSeriesId != null)
                {
                    if (!userPackage.CurrentSeriesId.Equals(episode.Series.Id))
                    {
                        if (userPackage.SeenEpisodes.Count != 0)
                        {
                            _traktApi.SendEpisodePlaystateUpdates(
                                userPackage.SeenEpisodes.ToList(),
                                traktUser,
                                true,
                                CancellationToken.None).ConfigureAwait(false);
                            userPackage.SeenEpisodes.Clear();
                        }

                        if (userPackage.UnSeenEpisodes.Count != 0)
                        {
                            _traktApi.SendEpisodePlaystateUpdates(
                                userPackage.UnSeenEpisodes.ToList(),
                                traktUser,
                                false,
                                CancellationToken.None).ConfigureAwait(false);
                            userPackage.UnSeenEpisodes.Clear();
                        }

                        userPackage.CurrentSeriesId = episode.Series.Id;
                    }
                    else
                    {
                        // Force update trakt.tv if we have more than 100 seen episodes in the queue due to API
                        if (userPackage.SeenEpisodes.Count >= 100)
                        {
                            _traktApi.SendEpisodePlaystateUpdates(
                                userPackage.SeenEpisodes.ToList(),
                                traktUser,
                                true,
                                CancellationToken.None).ConfigureAwait(false);
                            userPackage.SeenEpisodes.Clear();
                        }

                        // Force update trakt.tv if we have more than 100 unseen episodes in the queue due to API
                        if (userPackage.UnSeenEpisodes.Count >= 100)
                        {
                            _traktApi.SendEpisodePlaystateUpdates(
                                userPackage.UnSeenEpisodes.ToList(),
                                traktUser,
                                false,
                                CancellationToken.None).ConfigureAwait(false);
                            userPackage.UnSeenEpisodes.Clear();
                        }
                    }
                }

                if (traktUser.PostSetWatched && userDataSaveEventArgs.UserData.Played)
                {
                    userPackage.SeenEpisodes.Add(episode);
                }
                else if (traktUser.PostSetUnwatched)
                {
                    userPackage.UnSeenEpisodes.Add(episode);
                }
            }

            _userDataPackages[traktUser.LinkedMbUserId] = userPackage;
        }
    }

    private void OnTimerCallback(object state)
    {
        Dictionary<Guid, UserDataPackage> userDataQueue;
        lock (_userDataPackages)
        {
            if (_userDataPackages.Count == 0)
            {
                _logger.LogInformation("No events... stopping queue timer");
                return;
            }

            userDataQueue = new Dictionary<Guid, UserDataPackage>(_userDataPackages);
            _userDataPackages.Clear();
        }

        foreach (var package in userDataQueue)
        {
            if (package.Value.UnSeenMovies.Count != 0)
            {
                _traktApi.SendMoviePlaystateUpdates(
                    package.Value.UnSeenMovies.ToList(),
                    UserHelper.GetTraktUser(package.Key, true),
                    false,
                    CancellationToken.None).ConfigureAwait(false);
                package.Value.UnSeenMovies.Clear();
            }

            if (package.Value.SeenMovies.Count != 0)
            {
                _traktApi.SendMoviePlaystateUpdates(
                    package.Value.SeenMovies.ToList(),
                    UserHelper.GetTraktUser(package.Key, true),
                    true,
                    CancellationToken.None).ConfigureAwait(false);
                package.Value.SeenMovies.Clear();
            }

            if (package.Value.UnSeenEpisodes.Count != 0)
            {
                _traktApi.SendEpisodePlaystateUpdates(
                    package.Value.UnSeenEpisodes.ToList(),
                    UserHelper.GetTraktUser(package.Key),
                    false,
                    CancellationToken.None).ConfigureAwait(false);
                package.Value.UnSeenEpisodes.Clear();
            }

            if (package.Value.SeenEpisodes.Count != 0)
            {
                _traktApi.SendEpisodePlaystateUpdates(
                    package.Value.SeenEpisodes.ToList(),
                    UserHelper.GetTraktUser(package.Key),
                    true,
                    CancellationToken.None).ConfigureAwait(false);
                package.Value.SeenEpisodes.Clear();
            }
        }
    }

    public void Dispose()
    {
        _queueTimer?.Dispose();
    }
}
