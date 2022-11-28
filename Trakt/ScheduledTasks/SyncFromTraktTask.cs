using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Api.DataContracts.Sync.History;
using Trakt.Api.DataContracts.Users.Playback;
using Trakt.Api.DataContracts.Users.Watched;
using Trakt.Helpers;
using Episode = MediaBrowser.Controller.Entities.TV.Episode;

namespace Trakt.ScheduledTasks;

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
    /// Initializes a new instance of the <see cref="SyncFromTraktTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public SyncFromTraktTask(
        ILoggerFactory loggerFactory,
        IUserManager userManager,
        IUserDataManager userDataManager,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        ILibraryManager libraryManager)
    {
        _userManager = userManager;
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
        _logger = loggerFactory.CreateLogger<SyncFromTraktTask>();
        _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager);
    }

    /// <inheritdoc />
    public string Key => "TraktSyncFromTraktTask";

    /// <inheritdoc />
    public string Name => "Import watched states and playback progress from trakt.tv";

    /// <inheritdoc />
    public string Description => "Imports each user's watched/unwatched status and playback progress from trakt.tv to all items in the user's trakt.tv monitored locations";

    /// <inheritdoc />
    public string Category => "Trakt";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var users = _userManager.Users.Where(user => UserHelper.GetTraktUser(user, true) != null).ToList();

        // No point going further if we don't have users.
        if (users.Count == 0)
        {
            _logger.LogDebug("No Users returned");
            return;
        }

        // Purely for progress reporting
        var percentPerUser = 100d / users.Count;
        double currentProgress = 0;
        var numComplete = 0;

        foreach (var user in users)
        {
            try
            {
                await SyncTraktDataForUser(user, currentProgress, progress, percentPerUser, cancellationToken).ConfigureAwait(false);

                numComplete++;
                currentProgress = percentPerUser * numComplete;
                progress.Report(currentProgress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing trakt.tv data for user {UserName}", user.Username);
            }
        }
    }

    private async Task SyncTraktDataForUser(Jellyfin.Data.Entities.User user, double currentProgress, IProgress<double> progress, double percentPerUser, CancellationToken cancellationToken)
    {
        var traktUser = UserHelper.GetTraktUser(user, true);

        if (traktUser.SkipUnwatchedImportFromTrakt
            && traktUser.SkipWatchedImportFromTrakt
            && traktUser.SkipPlaybackProgressImportFromTrakt)
        {
            _logger.LogDebug("User {Name} disabled (un)watched and playback syncing.", user.Username);
            return;
        }

        List<TraktMovieWatched> traktWatchedMovies = new List<TraktMovieWatched>();
        List<TraktShowWatched> traktWatchedShows = new List<TraktShowWatched>();
        List<TraktMovieWatchedHistory> traktWatchedMoviesHistory = new List<TraktMovieWatchedHistory>(); // not used for now, just for reference to get watched movies history count
        List<TraktEpisodeWatchedHistory> traktWatchedEpisodesHistory = new List<TraktEpisodeWatchedHistory>(); // used for fall episode matching by ids
        List<TraktMoviePaused> traktPausedMovies = new List<TraktMoviePaused>();
        List<TraktEpisodePaused> traktPausedEpisodes = new List<TraktEpisodePaused>();

        try
        {
            /*
             * In order to be as accurate as possible. We need to download the user's show collection and the user's watched shows.
             * It's unfortunate that trakt.tv doesn't explicitly supply a bulk method to determine shows that have not been watched
             * like they do for movies.
             */
            if (!(traktUser.SkipUnwatchedImportFromTrakt && traktUser.SkipWatchedImportFromTrakt))
            {
                traktWatchedMovies.AddRange(await _traktApi.SendGetAllWatchedMoviesRequest(traktUser).ConfigureAwait(false));
                traktWatchedShows.AddRange(await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false));
                traktWatchedMoviesHistory.AddRange(await _traktApi.SendGetWatchedMoviesHistoryRequest(traktUser).ConfigureAwait(false));
                traktWatchedEpisodesHistory.AddRange(await _traktApi.SendGetWatchedEpisodesHistoryRequest(traktUser).ConfigureAwait(false));
            }

            if (!traktUser.SkipPlaybackProgressImportFromTrakt)
            {
                traktPausedMovies.AddRange(await _traktApi.SendGetAllPausedMoviesRequest(traktUser).ConfigureAwait(false));
                traktPausedEpisodes.AddRange(await _traktApi.SendGetPausedEpisodesRequest(traktUser).ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled");
            throw;
        }

        _logger.LogInformation("Trakt.tv watched movies for user {User}: {Count}", user.Username, traktWatchedMovies.Count);
        _logger.LogInformation("Trakt.tv watched movies history for user {User}: {Count}", user.Username, traktWatchedMoviesHistory.Count);
        _logger.LogInformation("Trakt.tv paused movies for user {User}: {Count}", user.Username, traktPausedMovies.Count);
        _logger.LogInformation("Trakt.tv watched shows for user {User}: {Count}", user.Username, traktWatchedShows.Count);
        _logger.LogInformation("Trakt.tv watched episodes history for user {User}: {Count}", user.Username, traktWatchedEpisodesHistory.Count);
        _logger.LogInformation("Trakt.tv paused episodes for user {User}: {Count}", user.Username, traktPausedEpisodes.Count);

        var baseQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[]
            {
                BaseItemKind.Movie,
                BaseItemKind.Episode
            },
            IsVirtualItem = false,
            OrderBy = new[]
            {
                (ItemSortBy.SeriesSortName, SortOrder.Ascending),
                (ItemSortBy.SortName, SortOrder.Ascending)
            }
        };

        var totalCount = _libraryManager.GetCount(baseQuery);

        const int Limit = 100;
        int offset = 0, previousCount;

        // Purely for progress reporting
        var percentPerIteration = percentPerUser / (totalCount / (double)Limit);

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var mediaItems = _libraryManager.GetItemList(baseQuery);

            previousCount = mediaItems.Count;
            offset += Limit;

            mediaItems = mediaItems.Where(i => _traktApi.CanSync(i, traktUser)).ToList();

            // Purely for progress reporting
            var percentPerItem = percentPerIteration / mediaItems.Count;

            foreach (var movie in mediaItems.OfType<Movie>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedWatchedMovie = Extensions.FindMatch(movie, traktWatchedMovies);
                var matchedPausedMovie = Extensions.FindMatch(movie, traktPausedMovies);
                var userData = _userDataManager.GetUserData(user.Id, movie);
                bool changed = false;

                if (matchedWatchedMovie != null)
                {
                    _logger.LogDebug("Movie is in watched list of user {User}: {Name}", user.Username, movie.Name);

                    if (!traktUser.SkipWatchedImportFromTrakt)
                    {
                        DateTime? tLastPlayed = null;
                        if (DateTime.TryParse(matchedWatchedMovie.LastWatchedAt, out var value))
                        {
                            tLastPlayed = value;
                        }

                        // Set movie as watched
                        if (!userData.Played)
                        {
                            // Only change LastPlayedDate if not set or the local and remote are more than 10 minutes apart
                            _logger.LogDebug("Marking movie as watched for user {User} locally: {Name}", user.Username, movie.Name);
                            if (tLastPlayed == null && userData.LastPlayedDate == null)
                            {
                                _logger.LogDebug("Movie's local and remote last played date are missing, falling back to the current time for user {User} locally: {Name}", user.Username, movie.Name);
                                userData.LastPlayedDate = DateTime.Now;
                            }

                            if (tLastPlayed != null
                                && userData.LastPlayedDate != null
                                && (tLastPlayed.Value - userData.LastPlayedDate.Value).Duration() > TimeSpan.FromMinutes(10)
                                && userData.LastPlayedDate < tLastPlayed)
                            {
                                _logger.LogDebug("Setting movie's last played date to remote which is more than 10 minutes more recent than local (remote:{Remote} | local:{Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, movie.Name);
                                userData.LastPlayedDate = tLastPlayed;
                            }

                            userData.Played = true;
                            changed = true;
                        }

                        // Keep the highest play count
                        if (userData.PlayCount < matchedWatchedMovie.Plays)
                        {
                            _logger.LogDebug("Adjusting movie's play count to match a higher remote value (remote:{Remote} | local:{Local}) for user {User} locally: {Name}", matchedWatchedMovie.Plays, userData.PlayCount, user.Username, movie.Name);
                            userData.PlayCount = matchedWatchedMovie.Plays;
                            changed = true;
                        }

                        // Update last played if remote time is more recent
                        if (tLastPlayed != null && userData.LastPlayedDate < tLastPlayed)
                        {
                            _logger.LogDebug("Adjusting movie's last played date to match a more recent remote last played date (remote:{Remote} | local:{Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, movie.Name);
                            userData.LastPlayedDate = tLastPlayed;
                            changed = true;
                        }
                    }
                }
                else if (!traktUser.SkipUnwatchedImportFromTrakt)
                {
                    _logger.LogDebug("Movie is not in watched list: {Name}", movie.Name);

                    // Set movie as unwatched
                    if (userData.Played)
                    {
                        _logger.LogDebug("Marking movie as unwatched for user {User} locally: {Name}", user.Username, movie.Name);
                        userData.Played = false;
                        changed = true;
                    }
                }

                if (!traktUser.SkipPlaybackProgressImportFromTrakt && matchedPausedMovie != null)
                {
                    _logger.LogDebug("Movie is in paused list of user {User}: {Name}", user.Username, movie.Name);

                    var lastPlayed = userData.LastPlayedDate;
                    DateTime? paused = null;
                    if (DateTime.TryParse(matchedPausedMovie.PausedAt, out var value))
                    {
                        paused = value;
                    }

                    if (lastPlayed == null || (paused != null && lastPlayed < paused))
                    {
                        _logger.LogDebug("Local last played date is missing or remote has more recent paused at date (remote:{Remote} | local:{Local}). Setting playback progress of movie for user {User} locally to {Progress}%: {Data}", paused, lastPlayed, user.Username, matchedPausedMovie.Progress, movie.Name);

                        var runtimeTicks = movie.GetRunTimeTicksForPlayState();
                        var traktPlaybackTicks = runtimeTicks != 0
                            ? (long)matchedPausedMovie.Progress * runtimeTicks / 100L
                            : 0;

                        userData.PlaybackPositionTicks = traktPlaybackTicks;
                        changed = true;
                    }
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

                // Purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }

            foreach (var episode in mediaItems.OfType<Episode>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedWatchedShow = Extensions.FindMatch(episode.Series, traktWatchedShows);
                var matchedWatchedEpisodeHistory = Extensions.FindMatch(episode, traktWatchedEpisodesHistory);
                var matchedPausedEpisode = Extensions.FindMatch(episode, traktPausedEpisodes);
                var userData = _userDataManager.GetUserData(user.Id, episode);
                bool changed = false;
                bool episodeWatched = false;

                if (!traktUser.SkipWatchedImportFromTrakt && matchedWatchedShow != null)
                {
                    var matchedWatchedSeason = matchedWatchedShow.Seasons.FirstOrDefault(tSeason => tSeason.Number == episode.GetSeasonNumber());

                    // Keep track of the shows rewatch cycles
                    DateTime? tLastReset = null;
                    if (DateTime.TryParse(matchedWatchedShow.ResetAt, out var resetValue))
                    {
                        tLastReset = resetValue;
                    }

                    // Fallback procedure to find match by using episode history
                    if (matchedWatchedSeason == null && matchedWatchedEpisodeHistory != null)
                    {
                        // Find watched season via history match
                        _logger.LogDebug("Using history to match season for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
                        matchedWatchedSeason = matchedWatchedShow.Seasons.FirstOrDefault(tSeason => tSeason.Number == matchedWatchedEpisodeHistory.Episode.Season);
                    }

                    // If it's not a match then it means trakt.tv doesn't know about the season, leave the watched state alone and move on
                    if (matchedWatchedSeason != null)
                    {
                        // Check for matching episodes including multi-episode entities
                        var matchedWatchedEpisode = matchedWatchedSeason.Episodes.FirstOrDefault(x => episode.ContainsEpisodeNumber(x.Number));

                        // Fallback procedure to find match by using episode history
                        if (matchedWatchedEpisode == null && matchedWatchedEpisodeHistory != null)
                        {
                            // Find watched season via history match
                            _logger.LogDebug("Using history to match episode for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
                            matchedWatchedEpisode = matchedWatchedSeason.Episodes.FirstOrDefault(tEpisode => tEpisode.Number == matchedWatchedEpisodeHistory.Episode.Number);
                        }

                        // Prepend a check if the matched episode is on a rewatch cycle and
                        // discard it if the last play date was before the reset date
                        if (matchedWatchedEpisode != null
                            && tLastReset != null
                            && DateTime.TryParse(matchedWatchedEpisode.LastWatchedAt, out var lastPlayedValue)
                            && lastPlayedValue < tLastReset)
                        {
                            matchedWatchedEpisode = null;
                        }

                        if (matchedWatchedEpisode != null)
                        {
                            _logger.LogDebug("Episode is in watched list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));

                            episodeWatched = true;
                            DateTime? tLastPlayed = null;
                            if (DateTime.TryParse(matchedWatchedEpisode.LastWatchedAt, out var lastWatchedValue))
                            {
                                tLastPlayed = lastWatchedValue;
                            }

                            // Set episode as watched
                            if (!userData.Played)
                            {
                                // Only change LastPlayedDate if not set or the local and remote are more than 10 minutes apart
                                _logger.LogDebug("Marking episode as watched for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                                if (tLastPlayed == null && userData.LastPlayedDate == null)
                                {
                                    _logger.LogDebug("Episode's local and remote last played date are missing, falling back to the current time for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                                    userData.LastPlayedDate = DateTime.Now;
                                }

                                if (tLastPlayed != null
                                    && userData.LastPlayedDate != null
                                    && (tLastPlayed.Value - userData.LastPlayedDate.Value).Duration() > TimeSpan.FromMinutes(10)
                                    && userData.LastPlayedDate < tLastPlayed)
                                {
                                    _logger.LogDebug("Setting episode's last played date to remote which is more than 10 minutes more recent than local (remote:{Remote} | local:{Local}) for user {User} locally: {Data}", tLastPlayed, userData.LastPlayedDate, user.Username, GetVerboseEpisodeData(episode));
                                    userData.LastPlayedDate = tLastPlayed;
                                }

                                userData.Played = true;
                                changed = true;
                            }

                            // Keep the highest play count
                            if (userData.PlayCount < matchedWatchedEpisode.Plays)
                            {
                                _logger.LogDebug("Adjusting episode's play count to match a higher remote value (remote:{Remote} | local:{Local}) for user {User} locally: {Data}", matchedWatchedEpisode.Plays, userData.PlayCount, user.Username, GetVerboseEpisodeData(episode));
                                userData.PlayCount = matchedWatchedEpisode.Plays;
                                changed = true;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No season data found for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
                    }
                }
                else
                {
                    _logger.LogDebug("No show data found for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
                }

                if (!traktUser.SkipUnwatchedImportFromTrakt && !episodeWatched)
                {
                    _logger.LogDebug("Episode not in watched list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));
                    if (userData.Played)
                    {
                        _logger.LogDebug("Marking episode as unwatched for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                        userData.Played = false;
                        changed = true;
                    }
                }

                if (!traktUser.SkipPlaybackProgressImportFromTrakt && matchedPausedEpisode != null)
                {
                    _logger.LogDebug("Episode is in paused list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));

                    var lastPlayed = userData.LastPlayedDate;
                    DateTime? paused = null;
                    if (DateTime.TryParse(matchedPausedEpisode.PausedAt, out var value))
                    {
                        paused = value;
                    }

                    if (lastPlayed == null || (paused != null && lastPlayed < paused))
                    {
                        _logger.LogDebug("Local last played date is missing or remote has more recent paused at date (remote:{Remote} | local:{Local}). Setting playback progress of episode for user {User} locally to {Progress}%: {Data}", paused, lastPlayed, user.Username, matchedPausedEpisode.Progress, GetVerboseEpisodeData(episode));

                        var runtimeTicks = episode.GetRunTimeTicksForPlayState();
                        var traktPlaybackTicks = runtimeTicks != 0
                            ? (long)matchedPausedEpisode.Progress * runtimeTicks / 100L
                            : 0;

                        userData.PlaybackPositionTicks = traktPlaybackTicks;
                        changed = true;
                    }
                }

                // Only process if changed
                if (changed)
                {
                    _userDataManager.SaveUserData(
                        user.Id,
                        episode,
                        userData,
                        UserDataSaveReason.Import,
                        cancellationToken);
                }

                // Purely for progress reporting
                currentProgress += percentPerItem;
                progress.Report(currentProgress);
            }
        }
        while (previousCount != 0);
    }

    private static string GetVerboseEpisodeData(Episode episode)
    {
        var episodeString = new StringBuilder()
            .Append("Episode: ")
            .Append(episode.GetSeasonNumber().ToString(CultureInfo.InvariantCulture))
            .Append('x')
            .Append(episode.IndexNumber != null ? episode.IndexNumber : "null")
            .Append(" '").Append(episode.Name).Append("' ")
            .Append("Series: '")
            .Append(episode.Series != null
                ? !string.IsNullOrWhiteSpace(episode.Series.Name)
                    ? episode.Series.Name
                    : "null property"
                : "null class")
            .Append("' ")
            .Append("Tvdb id: ")
            .Append(episode.GetProviderId(MetadataProvider.Tvdb) ?? "null").Append(' ')
            .Append("Tmdb id: ")
            .Append(episode.GetProviderId(MetadataProvider.Tmdb) ?? "null").Append(' ')
            .Append("Imdb id: ")
            .Append(episode.GetProviderId(MetadataProvider.Imdb) ?? "null").Append(' ')
            .Append("TvRage id: ")
            .Append(episode.GetProviderId(MetadataProvider.TvRage) ?? "null");

        return episodeString.ToString();
    }
}
