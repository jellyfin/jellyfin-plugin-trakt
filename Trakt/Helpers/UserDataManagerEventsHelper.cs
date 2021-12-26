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
/// Helper class used to update the watched status of movies/episodes. Attempts to organise
/// requests to lower trakt.tv api calls.
/// </summary>
internal class UserDataManagerEventsHelper : IDisposable
{
    private readonly ILogger<UserDataManagerEventsHelper> _logger;
    private readonly TraktApi _traktApi;
    private readonly List<UserDataPackage> _userDataPackages;
    private Timer _timer;

    /// <summary>
    ///
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="traktApi"></param>
    public UserDataManagerEventsHelper(ILogger<UserDataManagerEventsHelper> logger, TraktApi traktApi)
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
                if (traktUser.PostSetWatched)
                {
                    userPackage.SeenMovies.Add(movie);
                }

                if (userPackage.SeenMovies.Count >= 100)
                {
                    _traktApi.SendMoviePlaystateUpdates(
                        userPackage.SeenMovies,
                        userPackage.TraktUser,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
                    userPackage.SeenMovies = new List<Movie>();
                }
            }
            else
            {
                if (traktUser.PostSetUnwatched)
                {
                    userPackage.UnSeenMovies.Add(movie);
                }

                if (userPackage.UnSeenMovies.Count >= 100)
                {
                    _traktApi.SendMoviePlaystateUpdates(
                        userPackage.UnSeenMovies,
                        userPackage.TraktUser,
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
                    CancellationToken.None).ConfigureAwait(false);
                userPackage.SeenEpisodes = new List<Episode>();
            }

            if (userPackage.UnSeenEpisodes.Any())
            {
                _traktApi.SendEpisodePlaystateUpdates(
                    userPackage.UnSeenEpisodes,
                    userPackage.TraktUser,
                    false,
                    CancellationToken.None).ConfigureAwait(false);
                userPackage.UnSeenEpisodes = new List<Episode>();
            }

            userPackage.CurrentSeriesId = episode.Series.Id;
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
                var movies = package.UnSeenMovies.ToList();
                package.UnSeenMovies.Clear();
                _traktApi.SendMoviePlaystateUpdates(
                    movies,
                    package.TraktUser,
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
                    CancellationToken.None).ConfigureAwait(false);
            }

            if (package.UnSeenEpisodes.Any())
            {
                var episodes = package.UnSeenEpisodes.ToList();
                package.UnSeenEpisodes.Clear();
                _traktApi.SendEpisodePlaystateUpdates(
                    episodes,
                    package.TraktUser,
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
                    CancellationToken.None).ConfigureAwait(false);
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
            _timer.Dispose();
        }
    }
}
