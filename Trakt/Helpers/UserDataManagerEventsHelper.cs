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

namespace Trakt.Helpers
{
    /// <summary>
    /// Helper class used to update the watched status of movies/episodes.
    /// Attempts to organise requests to lower API calls.
    /// </summary>
    internal class UserDataManagerEventsHelper : IDisposable
    {
        private readonly ILogger<UserDataManagerEventsHelper> _logger;
        private readonly TraktApi _traktApi;
        private readonly List<UserDataPackage> _userDataPackages;
        private Timer _timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDataManagerEventsHelper"/> class.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{UserDataManagerEventsHelper}"/>.</param>
        /// <param name="traktApi">The <see cref="TraktApi"/>.</param>
        public UserDataManagerEventsHelper(ILogger<UserDataManagerEventsHelper> logger, TraktApi traktApi)
        {
            _userDataPackages = new List<UserDataPackage>();
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
            var userPackage = _userDataPackages.FirstOrDefault(user => user.TraktUser.Equals(traktUser));

            if (userPackage == null)
            {
                userPackage = new UserDataPackage
                {
                    TraktUser = traktUser
                };
            }

            if (_timer == null)
            {
                _timer = new Timer(
                    OnTimerCallback,
                    null,
                    TimeSpan.FromMilliseconds(3000),
                    Timeout.InfiniteTimeSpan);
            }
            else
            {
                _timer.Change(TimeSpan.FromMilliseconds(5000), Timeout.InfiniteTimeSpan);
            }

            if (userDataSaveEventArgs.Item is Movie movie)
            {
                if (userDataSaveEventArgs.UserData.Played)
                {
                    if (traktUser.PostSetWatched)
                    {
                        userPackage.SeenMovies.Add(movie);
                    }

                    // Force update trakt.tv if we have more than 100 seen movies in the queue due to API
                    if (userPackage.SeenMovies.Count >= 100)
                    {
                        _traktApi.SendMoviePlaystateUpdates(
                            userPackage.SeenMovies.ToList(),
                            userPackage.TraktUser,
                            true,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.SeenMovies.Clear();
                    }
                }
                else
                {
                    if (traktUser.PostSetUnwatched)
                    {
                        userPackage.UnSeenMovies.Add(movie);
                    }

                    // Force update trakt.tv if we have more than 100 unseen movies in the queue due to API
                    if (userPackage.UnSeenMovies.Count >= 100)
                    {
                        _traktApi.SendMoviePlaystateUpdates(
                            userPackage.UnSeenMovies.ToList(),
                            userPackage.TraktUser,
                            false,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.UnSeenMovies.Clear();
                    }
                }

                return;
            }

            if (!(userDataSaveEventArgs.Item is Episode episode))
            {
                return;
            }

            // If it's not the series we're currently storing, upload our episodes and reset the arrays
            if (userPackage.CurrentSeriesId != null)
            {
                if (!userPackage.CurrentSeriesId.Equals(episode.Series.Id))
                {
                    if (userPackage.SeenEpisodes.Any())
                    {
                        _traktApi.SendEpisodePlaystateUpdates(
                            userPackage.SeenEpisodes.ToList(),
                            userPackage.TraktUser,
                            true,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.SeenEpisodes.Clear();
                    }

                    if (userPackage.UnSeenEpisodes.Any())
                    {
                        _traktApi.SendEpisodePlaystateUpdates(
                            userPackage.UnSeenEpisodes.ToList(),
                            userPackage.TraktUser,
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
                            userPackage.TraktUser,
                            true,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.SeenEpisodes.Clear();
                    }

                    // Force update trakt.tv if we have more than 100 unseen episodes in the queue due to API
                    if (userPackage.UnSeenEpisodes.Count >= 100)
                    {
                        _traktApi.SendEpisodePlaystateUpdates(
                            userPackage.UnSeenEpisodes.ToList(),
                            userPackage.TraktUser,
                            false,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.UnSeenEpisodes.Clear();
                    }
                }
            }

            if (userDataSaveEventArgs.UserData.Played)
            {
                if (traktUser.PostSetWatched)
                {
                    userPackage.SeenEpisodes.Add(episode);
                }
            }
            else
            {
                if (traktUser.PostSetUnwatched)
                {
                    userPackage.UnSeenEpisodes.Add(episode);
                }
            }
        }

        private void OnTimerCallback(object state)
        {
            foreach (var package in _userDataPackages)
            {
                if (package.UnSeenMovies.Any())
                {
                    _traktApi.SendMoviePlaystateUpdates(
                        package.UnSeenMovies.ToList(),
                        package.TraktUser,
                        false,
                        CancellationToken.None).ConfigureAwait(false);
                    package.UnSeenMovies.Clear();
                }

                if (package.SeenMovies.Any())
                {
                    _traktApi.SendMoviePlaystateUpdates(
                        package.SeenMovies.ToList(),
                        package.TraktUser,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
                    package.SeenMovies.Clear();
                }

                if (package.UnSeenEpisodes.Any())
                {
                    _traktApi.SendEpisodePlaystateUpdates(
                        package.UnSeenEpisodes.ToList(),
                        package.TraktUser,
                        false,
                        CancellationToken.None).ConfigureAwait(false);
                    package.UnSeenEpisodes.Clear();
                }

                if (package.SeenEpisodes.Any())
                {
                    _traktApi.SendEpisodePlaystateUpdates(
                        package.SeenEpisodes.ToList(),
                        package.TraktUser,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
                    package.SeenEpisodes.Clear();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
            }
        }
    }
}
