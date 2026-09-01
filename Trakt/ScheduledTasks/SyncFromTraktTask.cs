using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Api.DataContracts.Sync.LastActivities;
using Trakt.Api.DataContracts.Users.Playback;
using Trakt.Api.DataContracts.Users.Watched;
using Trakt.Helpers;
using Trakt.Model;
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
        _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager, userManager);
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
        var users = _userManager.GetUsers().Where(user => UserHelper.GetTraktUser(user, true) != null).ToList();

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

    private async Task SyncTraktDataForUser(User user, double currentProgress, IProgress<double> progress, double percentPerUser, CancellationToken cancellationToken)
    {
        var traktUser = UserHelper.GetTraktUser(user, true);

        if (traktUser.SkipUnwatchedImportFromTrakt
            && traktUser.SkipWatchedImportFromTrakt
            && traktUser.SkipPlaybackProgressImportFromTrakt)
        {
            _logger.LogDebug("User {Name} disabled (un)watched and playback syncing.", user.Username);
            return;
        }

        var syncStartedAt = DateTime.UtcNow;
        TraktSyncLastActivities activities = null;

        try
        {
            activities = await _traktApi.SendGetLastActivitiesRequest(traktUser).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Locked)
        {
            _logger.LogError(ex, "Skipping sync for user {User} because their trakt.tv account is locked", user.Username);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't fetch last activities for user {User}, falling back to full sync", user.Username);
        }

        var watchedRelevant = !(traktUser.SkipWatchedImportFromTrakt && traktUser.SkipUnwatchedImportFromTrakt);
        var pausedRelevant = !traktUser.SkipPlaybackProgressImportFromTrakt;

        // Activity dates are server mutation times that can move backwards, so compare them for
        // inequality only. LastSyncFromTraktAt marks that a snapshot exists, so a stored null is
        // not read as changed forever.
        bool Changed(bool relevant, string stored, string current)
            => relevant && (activities == null
                || traktUser.LastSyncFromTraktAt == DateTime.MinValue
                || !string.Equals(stored ?? string.Empty, current ?? string.Empty, StringComparison.Ordinal));

        // DateLastSaved, not DateCreated: DateCreated can be backdated to the file creation time
        bool newMovies = true, newEpisodes = true;
        if (traktUser.LastSyncFromTraktAt != DateTime.MinValue)
        {
            newMovies = _libraryManager.GetCount(new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                IsVirtualItem = false,
                MinDateLastSaved = traktUser.LastSyncFromTraktAt
            }) > 0;

            newEpisodes = _libraryManager.GetCount(new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                IsVirtualItem = false,
                MinDateLastSaved = traktUser.LastSyncFromTraktAt
            }) > 0;
        }

        var movieSyncNeeded = Changed(watchedRelevant, traktUser.LastWatchedMoviesActivity, activities?.Movies?.WatchedAt)
            || Changed(pausedRelevant, traktUser.LastPausedMoviesActivity, activities?.Movies?.PausedAt)
            || newMovies;
        var episodeSyncNeeded = Changed(watchedRelevant, traktUser.LastWatchedEpisodesActivity, activities?.Episodes?.WatchedAt)
            || Changed(watchedRelevant, traktUser.LastHiddenShowsActivity, activities?.Shows?.HiddenAt)
            || Changed(pausedRelevant, traktUser.LastPausedEpisodesActivity, activities?.Episodes?.PausedAt)
            || newEpisodes;

        if (!movieSyncNeeded && !episodeSyncNeeded)
        {
            _logger.LogInformation("No trakt.tv activity and no new library items for user {User} since last sync, skipping import", user.Username);
            return;
        }

        List<TraktMovieWatched> traktWatchedMovies = new List<TraktMovieWatched>();
        List<TraktShowWatched> traktWatchedShows = new List<TraktShowWatched>();
        List<TraktWatchedEpisode> traktWatchedEpisodes = new List<TraktWatchedEpisode>();
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
                // Removals bump the same dates as additions, so a synced domain fetches its full
                // lists: the item loop unmarks anything missing from them.
                if (movieSyncNeeded)
                {
                    traktWatchedMovies.AddRange(await _traktApi.SendGetAllWatchedMoviesRequest(traktUser).ConfigureAwait(false));
                }

                if (episodeSyncNeeded)
                {
                    traktWatchedShows.AddRange(await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false));
                    traktWatchedEpisodes.AddRange(await _traktApi.SendGetWatchedEpisodesRequest(traktUser).ConfigureAwait(false));
                }
            }

            if (!traktUser.SkipPlaybackProgressImportFromTrakt)
            {
                if (movieSyncNeeded)
                {
                    traktPausedMovies.AddRange(await _traktApi.SendGetAllPausedMoviesRequest(traktUser).ConfigureAwait(false));
                }

                if (episodeSyncNeeded)
                {
                    traktPausedEpisodes.AddRange(await _traktApi.SendGetPausedEpisodesRequest(traktUser).ConfigureAwait(false));
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Locked)
        {
            _logger.LogError(ex, "Skipping sync for user {User} because their trakt.tv account is locked", user.Username);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled");
            throw;
        }

        _logger.LogInformation("Trakt.tv watched movies for user {User}: {Count}", user.Username, traktWatchedMovies.Count);
        _logger.LogInformation("Trakt.tv paused movies for user {User}: {Count}", user.Username, traktPausedMovies.Count);
        _logger.LogInformation("Trakt.tv watched shows for user {User}: {Count}", user.Username, traktWatchedShows.Count);
        _logger.LogInformation("Trakt.tv watched episodes for user {User}: {Count}", user.Username, traktWatchedEpisodes.Count);
        _logger.LogInformation("Trakt.tv paused episodes for user {User}: {Count}", user.Username, traktPausedEpisodes.Count);

        var watchedShowsProgressFetched = false;

        var includeItemTypes = new List<BaseItemKind>();
        if (movieSyncNeeded)
        {
            includeItemTypes.Add(BaseItemKind.Movie);
        }

        if (episodeSyncNeeded)
        {
            includeItemTypes.Add(BaseItemKind.Episode);
        }

        var baseQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = includeItemTypes.ToArray(),
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
        var percentPerIteration = totalCount > 0 ? percentPerUser / (totalCount / (double)Limit) : 0;

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var mediaItems = _libraryManager.GetItemList(baseQuery);

            previousCount = mediaItems.Count;
            offset += Limit;

            mediaItems = mediaItems.Where(i => _traktApi.CanSync(i, traktUser)).ToList();

            // Purely for progress reporting
            var percentPerItem = mediaItems.Count > 0 ? percentPerIteration / mediaItems.Count : 0;

            foreach (var movie in mediaItems.OfType<Movie>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matchedWatchedMovie = Extensions.FindMatch(movie, traktWatchedMovies);
                var matchedPausedMovie = Extensions.FindMatch(movie, traktPausedMovies);
                var userData = _userDataManager.GetUserData(user, movie);
                bool changed = false;

                if (matchedWatchedMovie != null)
                {
                    _logger.LogDebug("Movie is in watched list of user {User}: {Name}", user.Username, movie.Name);

                    if (!traktUser.SkipWatchedImportFromTrakt)
                    {
                        DateTime? tLastPlayed = null;
                        if (DateTimeOffset.TryParse(matchedWatchedMovie.LastWatchedAt, out var value))
                        {
                            tLastPlayed = value.UtcDateTime;
                        }

                        // Set movie as watched
                        if (!userData.Played)
                        {
                            // Only change LastPlayedDate if not set or the local and remote are more than 10 minutes apart
                            _logger.LogDebug("Marking movie as watched for user {User} locally: {Name}", user.Username, movie.Name);
                            if (tLastPlayed == null && userData.LastPlayedDate == null)
                            {
                                _logger.LogDebug("Movie's local and remote last played date are missing, falling back to the current time for user {User} locally: {Name}", user.Username, movie.Name);
                                userData.LastPlayedDate = DateTime.UtcNow;
                            }

                            if (tLastPlayed != null
                                && userData.LastPlayedDate != null
                                && (tLastPlayed.Value - userData.LastPlayedDate.Value).Duration() > TimeSpan.FromMinutes(10)
                                && userData.LastPlayedDate < tLastPlayed)
                            {
                                _logger.LogDebug("Setting movie's last played date to remote which is more than 10 minutes more recent than local (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, movie.Name);
                                userData.LastPlayedDate = tLastPlayed;
                            }

                            userData.Played = true;
                            changed = true;
                        }

                        // Keep the highest play count
                        if (userData.PlayCount < matchedWatchedMovie.Plays)
                        {
                            _logger.LogDebug("Adjusting movie's play count to match a higher remote value (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", matchedWatchedMovie.Plays, userData.PlayCount, user.Username, movie.Name);
                            userData.PlayCount = matchedWatchedMovie.Plays;
                            changed = true;
                        }

                        // Update last played if remote time is more recent
                        if (tLastPlayed != null && (userData.LastPlayedDate == null || userData.LastPlayedDate < tLastPlayed))
                        {
                            _logger.LogDebug("Adjusting movie's last played date to match a more recent remote last played date (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, movie.Name);
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
                    if (DateTimeOffset.TryParse(matchedPausedMovie.PausedAt, out var value))
                    {
                        paused = value.UtcDateTime;
                    }

                    if (lastPlayed == null || (paused != null && lastPlayed < paused))
                    {
                        _logger.LogDebug("Local last played date is missing or remote has more recent paused at date (remote: {Remote} | local: {Local}). Setting playback progress of movie for user {User} locally to {Progress}%: {Data}", paused, lastPlayed, user.Username, matchedPausedMovie.Progress, movie.Name);

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
                        user,
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
                var matchedPausedEpisode = Extensions.FindMatch(episode, traktPausedEpisodes);
                var userData = _userDataManager.GetUserData(user, episode);
                bool changed = false;
                bool episodeWatched = false;

                if (!traktUser.SkipWatchedImportFromTrakt && matchedWatchedShow != null)
                {
                    // Keep track of the shows rewatch cycles
                    DateTime? tLastReset = null;
                    if (DateTimeOffset.TryParse(matchedWatchedShow.ResetAt, out var resetValue))
                    {
                        tLastReset = resetValue.UtcDateTime;
                    }

                    var matchedWatchedEpisode = Extensions.FindMatch(episode, traktWatchedEpisodes);

                    // /sync/watched/episodes has no show object, so id-less episodes only
                    // match by season/episode against show progress
                    if (matchedWatchedEpisode == null && !Extensions.HasAnyProviderTvId(episode))
                    {
                        if (!watchedShowsProgressFetched)
                        {
                            watchedShowsProgressFetched = true;
                            await FetchWatchedShowsProgress(traktWatchedShows, traktUser, user, cancellationToken).ConfigureAwait(false);
                            matchedWatchedShow = Extensions.FindMatch(episode.Series, traktWatchedShows);
                        }

                        matchedWatchedEpisode = Extensions.FindMatchFromShowProgress(episode, matchedWatchedShow);
                    }

                    DateTime? tLastPlayed = null;
                    if (matchedWatchedEpisode != null
                        && DateTimeOffset.TryParse(matchedWatchedEpisode.LastWatchedAt, out var lastWatchedValue))
                    {
                        tLastPlayed = lastWatchedValue.UtcDateTime;
                    }

                    // Discard the match if the episode is on a rewatch cycle and
                    // the last play date was before the reset date
                    if (matchedWatchedEpisode != null
                        && tLastReset != null
                        && tLastPlayed != null
                        && tLastPlayed < tLastReset)
                    {
                        matchedWatchedEpisode = null;
                    }

                    if (matchedWatchedEpisode != null)
                    {
                        _logger.LogDebug("Episode is in watched list of user {User}: {Data}", user.Username, GetVerboseEpisodeData(episode));

                        episodeWatched = true;

                        // Set episode as watched
                        if (!userData.Played)
                        {
                            // Only change LastPlayedDate if not set or the local and remote are more than 10 minutes apart
                            _logger.LogDebug("Marking episode as watched for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                            if (tLastPlayed == null && userData.LastPlayedDate == null)
                            {
                                _logger.LogDebug("Episode's local and remote last played date are missing, falling back to the current time for user {User} locally: {Data}", user.Username, GetVerboseEpisodeData(episode));
                                userData.LastPlayedDate = DateTime.UtcNow;
                            }

                            if (tLastPlayed != null
                                && userData.LastPlayedDate != null
                                && (tLastPlayed.Value - userData.LastPlayedDate.Value).Duration() > TimeSpan.FromMinutes(10)
                                && userData.LastPlayedDate < tLastPlayed)
                            {
                                _logger.LogDebug("Setting episode's last played date to remote which is more than 10 minutes more recent than local (remote: {Remote} | local: {Local}) for user {User} locally: {Data}", tLastPlayed, userData.LastPlayedDate, user.Username, GetVerboseEpisodeData(episode));
                                userData.LastPlayedDate = tLastPlayed;
                            }

                            userData.Played = true;
                            changed = true;
                        }

                        // Update last played if remote time is more recent
                        if (tLastPlayed != null && (userData.LastPlayedDate == null || userData.LastPlayedDate < tLastPlayed))
                        {
                            _logger.LogDebug("Adjusting episode's last played date to match a more recent remote last played date (remote: {Remote} | local: {Local}) for user {User} locally: {Name}", tLastPlayed, userData.LastPlayedDate, user.Username, episode.Name);
                            userData.LastPlayedDate = tLastPlayed;
                            changed = true;
                        }

                        // Keep the highest play count
                        if (userData.PlayCount < matchedWatchedEpisode.Plays)
                        {
                            _logger.LogDebug("Adjusting episode's play count to match a higher remote value (remote: {Remote} | local: {Local}) for user {User} locally: {Data}", matchedWatchedEpisode.Plays, userData.PlayCount, user.Username, GetVerboseEpisodeData(episode));
                            userData.PlayCount = matchedWatchedEpisode.Plays;
                            changed = true;
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No watched episode data found for user {User} for {Data}", user.Username, GetVerboseEpisodeData(episode));
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
                    if (DateTimeOffset.TryParse(matchedPausedEpisode.PausedAt, out var value))
                    {
                        paused = value.UtcDateTime;
                    }

                    if (lastPlayed == null || (paused != null && lastPlayed < paused))
                    {
                        _logger.LogDebug("Local last played date is missing or remote has more recent paused at date (remote: {Remote} | local: {Local}). Setting playback progress of episode for user {User} locally to {Progress}%: {Data}", paused, lastPlayed, user.Username, matchedPausedEpisode.Progress, GetVerboseEpisodeData(episode));

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
                        user,
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

        if (activities != null)
        {
            if (watchedRelevant)
            {
                traktUser.LastWatchedMoviesActivity = activities.Movies?.WatchedAt;
                traktUser.LastWatchedEpisodesActivity = activities.Episodes?.WatchedAt;
                traktUser.LastHiddenShowsActivity = activities.Shows?.HiddenAt;
            }

            if (pausedRelevant)
            {
                traktUser.LastPausedMoviesActivity = activities.Movies?.PausedAt;
                traktUser.LastPausedEpisodesActivity = activities.Episodes?.PausedAt;
            }

            traktUser.LastSyncFromTraktAt = syncStartedAt;
            Plugin.Instance.SaveConfiguration();
        }
    }

    private async Task FetchWatchedShowsProgress(List<TraktShowWatched> traktWatchedShows, TraktUser traktUser, User user, CancellationToken cancellationToken)
    {
        try
        {
            var watchedShowsProgress = await _traktApi.SendGetWatchedShowsProgressRequest(traktUser, cancellationToken).ConfigureAwait(false);
            traktWatchedShows.Clear();
            traktWatchedShows.AddRange(watchedShowsProgress);
            _logger.LogInformation("Trakt.tv watched shows progress for user {User}: {Count}", user.Username, watchedShowsProgress.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch watched shows progress for user {User} - episodes without provider ids will not be matched", user.Username);
        }
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
