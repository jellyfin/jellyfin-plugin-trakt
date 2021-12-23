using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Jellyfin.Data.Enums;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.Users.Collection;
using Trakt.Api.DataContracts.Users.Watched;
using Trakt.Helpers;

namespace Trakt.ScheduledTasks
{
    /// <summary>
    /// Task that will Sync each users trakt.tv profile with their local library. This task will only include
    /// watched states.
    /// </summary>
    public class SyncFromTraktTask : IScheduledTask
    {
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<SyncFromTraktTask> _logger;
        private readonly TraktApi _traktApi;

        /// <summary>
        ///
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="userManager"></param>
        /// <param name="userDataManager"> </param>
        /// <param name="httpClient"></param>
        /// <param name="appHost"></param>
        /// <param name="fileSystem"></param>
        public SyncFromTraktTask(
            ILoggerFactory loggerFactory,
            IUserManager userManager,
            IUserDataManager userDataManager,
            IHttpClientFactory httpClientFactory,
            IServerApplicationHost appHost,
            IFileSystem fileSystem,
            ILibraryManager libraryManager)
        {
            _userManager = userManager;
            _userDataManager = userDataManager;
            _libraryManager = libraryManager;
            _logger = loggerFactory.CreateLogger<SyncFromTraktTask>();
            _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, fileSystem);
        }

        public string Key => "TraktSyncFromTraktTask";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

        public string Name => "Import playstates from Trakt.tv";

        public string Description => "Sync Watched/Unwatched status from Trakt.tv for each Jellyfin user that has a configured Trakt account";

        public string Category => "Trakt";

        /// <summary>
        /// Gather users and call <see cref="SyncTraktDataForUser"/>
        /// </summary>
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var users = _userManager.Users.Where(u => UserHelper.GetTraktUser(u) != null).ToList();

            // No point going further if we don't have users.
            if (users.Count == 0)
            {
                _logger.LogInformation("No Users returned");
                return;
            }

            // purely for progress reporting
            var percentPerUser = 100 / users.Count;
            double currentProgress = 0;
            var numComplete = 0;

            foreach (var user in users)
            {
                try
                {
                    await SyncTraktDataForUser(user, currentProgress, cancellationToken, progress, percentPerUser).ConfigureAwait(false);

                    numComplete++;
                    currentProgress = percentPerUser * numComplete;
                    progress.Report(currentProgress);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing trakt data for user {UserName}", user.Username);
                }
            }
        }

        private async Task SyncTraktDataForUser(Jellyfin.Data.Entities.User user, double currentProgress, CancellationToken cancellationToken, IProgress<double> progress, double percentPerUser)
        {
            var traktUser = UserHelper.GetTraktUser(user);

            List<TraktMovieWatched> traktWatchedMovies;
            List<TraktShowWatched> traktWatchedShows;

            try
            {
                /*
                 * In order to be as accurate as possible. We need to download the users show collection & the users watched shows.
                 * It's unfortunate that trakt.tv doesn't explicitly supply a bulk method to determine shows that have not been watched
                 * like they do for movies.
                 */
                traktWatchedMovies = await _traktApi.SendGetAllWatchedMoviesRequest(traktUser).ConfigureAwait(false);
                traktWatchedShows = await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handled");
                throw;
            }

            _logger.LogInformation("Trakt.tv watched Movies count = " + traktWatchedMovies.Count);
            _logger.LogInformation("Trakt.tv watched Shows count = " + traktWatchedShows.Count);

            var mediaItems =
                _libraryManager.GetItemList(
                        new InternalItemsQuery(user)
                        {
                            IncludeItemTypes = new[] { typeof(Movie).Name, typeof(Episode).Name },
                            IsVirtualItem = false,
                            OrderBy = new[]
                            {
                                new ValueTuple<string, SortOrder>(ItemSortBy.SeriesSortName, SortOrder.Ascending),
                                new ValueTuple<string, SortOrder>(ItemSortBy.SortName, SortOrder.Ascending)
                            }
                        })
                    .Where(i => _traktApi.CanSync(i, traktUser)).ToList();

            // purely for progress reporting
            var percentPerItem = percentPerUser / mediaItems.Count;

            foreach (var movie in mediaItems.OfType<Movie>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedMovie = FindMatch(movie, traktWatchedMovies);

                if (matchedMovie != null)
                {
                    _logger.LogDebug("Movie is in Watched list " + movie.Name);

                    var userData = _userDataManager.GetUserData(user.Id, movie);
                    bool changed = false;

                    DateTime? tLastPlayed = null;
                    if (DateTime.TryParse(matchedMovie.last_watched_at, out var value))
                    {
                        tLastPlayed = value;
                    }

                    // set movie as watched
                    if (!userData.Played)
                    {
                        userData.Played = true;
                        userData.LastPlayedDate = tLastPlayed ?? DateTime.Now;
                        changed = true;
                    }

                    // keep the highest play count
                    if (userData.PlayCount < matchedMovie.plays)
                    {
                        userData.PlayCount = matchedMovie.plays;
                        changed = true;
                    }

                    // Update last played if remote time is more recent
                    if (tLastPlayed != null && userData.LastPlayedDate < tLastPlayed)
                    {
                        userData.LastPlayedDate = tLastPlayed;
                        changed = true;
                    }

                    // Only process if there's a change
                    if (changed)
                    {
                        _userDataManager.SaveUserData(
                               user.Id,
                               movie,
                               userData,
                               UserDataSaveReason.Import,
                               cancellationToken);
                    }
                }
                else
                {
                    //_logger.LogInformation("Failed to match " + movie.Name);
                }

                // purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }

            foreach (var episode in mediaItems.OfType<Episode>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedShow = FindMatch(episode.Series, traktWatchedShows);

                if (matchedShow != null)
                {
                    var matchedSeason =
                        matchedShow.seasons.FirstOrDefault(
                            tSeason =>
                                tSeason.number
                                == (episode.ParentIndexNumber == 0
                                        ? 0
                                        : (episode.ParentIndexNumber ?? 1)));

                    // keep track of the shows rewatch cycles
                    DateTime? tLastReset = null;
                    if (DateTime.TryParse(matchedShow.reset_at, out var resetValue))
                    {
                        tLastReset = resetValue;
                    }

                    // if it's not a match then it means trakt doesn't know about the season, leave the watched state alone and move on
                    if (matchedSeason != null)
                    {
                        // episode is in users libary. Now we need to determine if it's watched
                        var userData = _userDataManager.GetUserData(user.Id, episode);
                        bool changed = false;

                        var matchedEpisode =
                            matchedSeason.episodes.FirstOrDefault(x => x.number == (episode.IndexNumber ?? -1));

                        // prepend a check if the matched episode is on a rewatch cycle and 
                        // discard it if the last play date was before the reset date
                        if (matchedEpisode != null && tLastReset != null)
                        {
                            if (DateTime.TryParse(matchedEpisode.last_watched_at, out var value) && value < tLastReset)
                            {
                                matchedEpisode = null;
                            }
                        }

                        if (matchedEpisode != null)
                        {
                            _logger.LogDebug("Episode is in Watched list " + GetVerboseEpisodeData(episode));

                            if(!traktUser.SkipWatchedImportFromTrakt)
                            {
                                DateTime? tLastPlayed = null;
                                if (DateTime.TryParse(matchedEpisode.last_watched_at, out var value))
                                {
                                    tLastPlayed = value;
                                }

                                // Set episode as watched
                                if (!userData.Played)
                                {
                                    userData.Played = true;
                                    userData.LastPlayedDate = tLastPlayed ?? DateTime.Now;
                                    changed = true;
                                }

                                // keep the highest play count
                                if (userData.PlayCount < matchedEpisode.plays)
                                {
                                    userData.PlayCount = matchedEpisode.plays;
                                    changed = true;
                                }

                                // Update last played if remote time is more recent
                                if (tLastPlayed != null && userData.LastPlayedDate < tLastPlayed)
                                {
                                    userData.LastPlayedDate = tLastPlayed;
                                    changed = true;
                                }
                            }
                        }
                        else if (!traktUser.SkipUnwatchedImportFromTrakt)
                        {
                            userData.Played = false;
                            userData.PlayCount = 0;
                            userData.LastPlayedDate = null;
                            changed = true;
                        }

                        // only process if changed
                        if (changed)
                        {
                            _userDataManager.SaveUserData(
                                user.Id,
                                episode,
                                userData,
                                UserDataSaveReason.Import,
                                cancellationToken);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No Season match in Watched shows list " + GetVerboseEpisodeData(episode));
                    }
                }
                else
                {
                    _logger.LogDebug("No Show match in Watched shows list " + GetVerboseEpisodeData(episode));
                }

                // purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }

            // _logger.LogInformation(syncItemFailures + " items not parsed");
        }

        private static string GetVerboseEpisodeData(Episode episode)
        {
            var episodeString = new StringBuilder()
                .Append("Episode: ")
                .Append(episode.ParentIndexNumber != null ? episode.ParentIndexNumber.ToString() : "null")
                .Append("x")
                .Append(episode.IndexNumber != null ? episode.IndexNumber.ToString() : "null")
                .Append(" '").Append(episode.Name).Append("' ")
                .Append("Series: '")
                .Append(episode.Series != null
                    ? !string.IsNullOrWhiteSpace(episode.Series.Name)
                        ? episode.Series.Name
                        : "null property"
                    : "null class")
                .Append('\'');

            return episodeString.ToString();
        }

        public static TraktShowWatched FindMatch(Series item, IEnumerable<TraktShowWatched> results)
        {
            return results.FirstOrDefault(i => IsMatch(item, i.show));
        }

        public static TraktShowCollected FindMatch(Series item, IEnumerable<TraktShowCollected> results)
        {
            return results.FirstOrDefault(i => IsMatch(item, i.show));
        }

        public static TraktMovieWatched FindMatch(BaseItem item, IEnumerable<TraktMovieWatched> results)
        {
            return results.FirstOrDefault(i => IsMatch(item, i.movie));
        }

        public static IEnumerable<TraktMovieCollected> FindMatches(BaseItem item, IEnumerable<TraktMovieCollected> results)
        {
            return results.Where(i => IsMatch(item, i.movie)).ToList();
        }

        public static bool IsMatch(BaseItem item, TraktMovie movie)
        {
            var imdb = item.GetProviderId(MetadataProvider.Imdb);

            if (!string.IsNullOrWhiteSpace(imdb) &&
                string.Equals(imdb, movie.ids.imdb, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var tmdb = item.GetProviderId(MetadataProvider.Tmdb);

            if (movie.ids.tmdb.HasValue && string.Equals(tmdb, movie.ids.tmdb.Value.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (item.Name == movie.title && item.ProductionYear == movie.year)
            {
                return true;
            }

            return false;
        }

        public static bool IsMatch(Series item, TraktShow show)
        {
            var tvdb = item.GetProviderId(MetadataProvider.Tvdb);
            if (!string.IsNullOrWhiteSpace(tvdb) &&
                string.Equals(tvdb, show.ids.tvdb.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var imdb = item.GetProviderId(MetadataProvider.Imdb);
            if (!string.IsNullOrWhiteSpace(imdb) &&
                string.Equals(imdb, show.ids.imdb, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
