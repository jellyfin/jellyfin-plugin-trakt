﻿using System;
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
    /// Helper class used to update the watched status of movies/episodes. Attempts to organise
    /// requests to lower trakt.tv api calls.
    /// </summary>
    internal class UserDataManagerEventsHelper
    {
        private List<UserDataPackage> _userDataPackages;
        private readonly ILogger _logger;
        private readonly TraktApi _traktApi;
        private Timer _timer;

        /// <summary>
        ///
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="traktApi"></param>
        public UserDataManagerEventsHelper(ILogger logger, TraktApi traktApi)
        {
            _userDataPackages = new List<UserDataPackage>();
            _logger = logger;
            _traktApi = traktApi;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="userDataSaveEventArgs"></param>
        /// <param name="traktUser"></param>
        public void ProcessUserDataSaveEventArgs(UserDataSaveEventArgs userDataSaveEventArgs, TraktUser traktUser)
        {
            var userPackage = _userDataPackages.FirstOrDefault(e => e.TraktUser.Equals(traktUser));

            if (userPackage == null)
            {
                userPackage = new UserDataPackage { TraktUser = traktUser };
                _userDataPackages.Add(userPackage);
            }


            if (_timer == null)
            {
                _timer = new Timer(
                    OnTimerCallback,
                    null,
                    TimeSpan.FromMilliseconds(5000),
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
                    userPackage.SeenMovies.Add(movie);

                    if (userPackage.SeenMovies.Count >= 100)
                    {
                        _traktApi.SendMoviePlaystateUpdates(
                            userPackage.SeenMovies,
                            userPackage.TraktUser,
                            true,
                            true,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.SeenMovies = new List<Movie>();
                    }
                }
                else
                {
                    userPackage.UnSeenMovies.Add(movie);

                    if (userPackage.UnSeenMovies.Count >= 100)
                    {
                        _traktApi.SendMoviePlaystateUpdates(
                            userPackage.UnSeenMovies,
                            userPackage.TraktUser,
                            true,
                            false,
                            CancellationToken.None).ConfigureAwait(false);
                        userPackage.UnSeenMovies = new List<Movie>();
                    }
                }

                return;
            }


            if (!(userDataSaveEventArgs.Item is Episode episode))
            {
                return;
            }

            // If it's not the series we're currently storing, upload our episodes and reset the arrays
            if (!userPackage.CurrentSeriesId.Equals(episode.Series.Id))
            {
                if (userPackage.SeenEpisodes.Any())
                {
                    _traktApi.SendEpisodePlaystateUpdates(
                        userPackage.SeenEpisodes,
                        userPackage.TraktUser,
                        true,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
                    userPackage.SeenEpisodes = new List<Episode>();
                }

                if (userPackage.UnSeenEpisodes.Any())
                {
                    _traktApi.SendEpisodePlaystateUpdates(
                        userPackage.UnSeenEpisodes,
                        userPackage.TraktUser,
                        true,
                        false,
                        CancellationToken.None).ConfigureAwait(false);
                    userPackage.UnSeenEpisodes = new List<Episode>();
                }

                userPackage.CurrentSeriesId = episode.Series.Id;
            }

            if (userDataSaveEventArgs.UserData.Played)
            {
                userPackage.SeenEpisodes.Add(episode);
            }
            else
            {
                userPackage.UnSeenEpisodes.Add(episode);
            }
        }

        private void OnTimerCallback(object state)
        {
            foreach (var package in _userDataPackages)
            {
                if (package.UnSeenMovies.Any())
                {
                    var movies = package.UnSeenMovies.ToList();
                    package.UnSeenMovies.Clear();
                    _traktApi.SendMoviePlaystateUpdates(
                        movies,
                        package.TraktUser,
                        true,
                        false,
                        CancellationToken.None).ConfigureAwait(false);
                }

                if (package.SeenMovies.Any())
                {
                    var movies = package.SeenMovies.ToList();
                    package.SeenMovies.Clear();
                    _traktApi.SendMoviePlaystateUpdates(
                        movies,
                        package.TraktUser,
                        true,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
                }
                if (package.UnSeenEpisodes.Any())
                {
                    var episodes = package.UnSeenEpisodes.ToList();
                    package.UnSeenEpisodes.Clear();
                    _traktApi.SendEpisodePlaystateUpdates(
                        episodes,
                        package.TraktUser,
                        true,
                        false,
                        CancellationToken.None).ConfigureAwait(false);
                }
                if (package.SeenEpisodes.Any())
                {
                    var episodes = package.SeenEpisodes.ToList();
                    package.SeenEpisodes.Clear();
                    _traktApi.SendEpisodePlaystateUpdates(
                        episodes,
                        package.TraktUser,
                        true,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Class that contains all the items to be reported to trakt.tv and supporting properties.
    /// </summary>
    internal class UserDataPackage
    {
        public TraktUser TraktUser { get; set; }

        public Guid CurrentSeriesId { get; set; }

        public List<Movie> SeenMovies { get; set; }

        public List<Movie> UnSeenMovies { get; set; }

        public List<Episode> SeenEpisodes { get; set; }

        public List<Episode> UnSeenEpisodes { get; set; }

        public UserDataPackage()
        {
            SeenMovies = new List<Movie>();
            UnSeenMovies = new List<Movie>();
            SeenEpisodes = new List<Episode>();
            UnSeenEpisodes = new List<Episode>();
        }
    }
}
