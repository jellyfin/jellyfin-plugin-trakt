using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Trakt.Api;
using Trakt.Api.DataContracts.Sync;
using Trakt.Api.Enums;
using Trakt.Helpers;
using Trakt.Model;
using Trakt.Model.Enums;

namespace Trakt.ScheduledTasks;

/// <summary>
/// Task that will Sync each users local library with their respective trakt.tv profiles. This task will only include
/// titles, watched states will be synced in other tasks.
/// </summary>
public class SyncLibraryTask : IScheduledTask
{
    private readonly IUserManager _userManager;

    private readonly ILogger<SyncLibraryTask> _logger;

    private readonly TraktApi _traktApi;

    private readonly IUserDataManager _userDataManager;

    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncLibraryTask"/> class.
    /// </summary>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public SyncLibraryTask(
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
        _logger = loggerFactory.CreateLogger<SyncLibraryTask>();
        _traktApi = new TraktApi(loggerFactory.CreateLogger<TraktApi>(), httpClientFactory, appHost, userDataManager);
    }

    /// <inheritdoc />
    public string Key => "TraktSyncLibraryTask";

    /// <inheritdoc />
    public string Name => "Export library to trakt.tv";

    /// <inheritdoc />
    public string Category => "Trakt";

    /// <inheritdoc />
    public string Description => "Exports any media that is in each user's trakt.tv monitored locations to their trakt.tv collection";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var users = _userManager.Users.Where(u => UserHelper.GetTraktUser(u) != null).ToList();

        // No point going further if we don't have users.
        if (users.Count == 0)
        {
            _logger.LogInformation("No Users returned");
            return;
        }

        // Purely for progress reporting
        double currentProgress = 0;
        var percentPerUser = 100d / users.Count;

        foreach (var user in users)
        {
            var traktUser = UserHelper.GetTraktUser(user);

            if (!(traktUser.SynchronizeCollections || traktUser.PostUnwatchedHistory || traktUser.PostWatchedHistory))
            {
                _logger.LogDebug("User {Name} disabled collection and history syncing.", user.Username);
                continue;
            }

            await SyncUserLibrary(user, traktUser, progress, currentProgress, percentPerUser, cancellationToken).ConfigureAwait(false);

            currentProgress += percentPerUser;
            progress.Report(currentProgress);
        }
    }

    /// <summary>
    /// Calls <see cref="SyncMovies"/> and <see cref="SyncShows"/>.
    /// </summary>
    /// <param name="user">The <see cref="Jellyfin.Data.Entities.User"/>.</param>
    /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="currentProgress">The current progress.</param>
    /// <param name="percentPerUser">Percent per user.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>Task.</returns>
    private async Task SyncUserLibrary(
        Jellyfin.Data.Entities.User user,
        TraktUser traktUser,
        IProgress<double> progress,
        double currentProgress,
        double percentPerUser,
        CancellationToken cancellationToken)
    {
        var partialPercentage = percentPerUser * 0.5;
        await SyncMovies(user, traktUser, progress, currentProgress, partialPercentage, cancellationToken).ConfigureAwait(false);
        await SyncShows(user, traktUser, progress, currentProgress + partialPercentage, partialPercentage, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncMovies(
        Jellyfin.Data.Entities.User user,
        TraktUser traktUser,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        List<Api.DataContracts.Users.Watched.TraktMovieWatched> traktWatchedMovies = new List<Api.DataContracts.Users.Watched.TraktMovieWatched>();
        List<Api.DataContracts.Users.Collection.TraktMovieCollected> traktCollectedMovies = new List<Trakt.Api.DataContracts.Users.Collection.TraktMovieCollected>();

        try
        {
            /*
            * In order to sync watched status to trakt.tv we need to know what's been watched on trakt.tv already. This
            * will stop us from endlessly incrementing the watched values on the site.
            */
            if (traktUser.PostWatchedHistory || traktUser.PostUnwatchedHistory)
            {
                traktWatchedMovies.AddRange(await _traktApi.SendGetAllWatchedMoviesRequest(traktUser).ConfigureAwait(false));
            }

            if (traktUser.SynchronizeCollections)
            {
                traktCollectedMovies.AddRange(await _traktApi.SendGetAllCollectedMoviesRequest(traktUser).ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled in SyncMovies");
            throw;
        }

        var baseQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            IsVirtualItem = false,
            OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
        };

        // Purely for progress reporting
        availablePercent /= 4;

        var collectedMovies = new List<Movie>();
        var playedMovies = new List<Movie>();
        var unplayedMovies = new List<Movie>();

        const int Limit = 100;
        int offset = 0, previousCount;

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var items = _libraryManager.GetItemList(baseQuery);
            previousCount = items.Count;
            offset += Limit;
            var movieItems = items.OfType<Movie>().Where(x => _traktApi.CanSync(x, traktUser));

            if (movieItems != null)
            {
                foreach (var libraryMovie in movieItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var userData = _userDataManager.GetUserData(user.Id, libraryMovie);

                    if (traktUser.SynchronizeCollections)
                    {
                        // If movie is not collected, or (export media info setting is enabled and every collected matching movie has different metadata), collect it
                        var collectedMatchingMovies = Extensions.FindMatch(libraryMovie, traktCollectedMovies);
                        if (collectedMatchingMovies == null
                            || (traktUser.ExportMediaInfo && collectedMatchingMovies.MetadataIsDifferent(libraryMovie)))
                        {
                            collectedMovies.Add(libraryMovie);
                        }
                    }

                    var movieWatched = Extensions.FindMatch(libraryMovie, traktWatchedMovies);

                    // If the movie has been played locally and is unplayed on trakt.tv then add it to the list
                    if (userData.Played)
                    {
                        if (movieWatched == null)
                        {
                            if (traktUser.PostWatchedHistory)
                            {
                                playedMovies.Add(libraryMovie);
                            }

                            if (!traktUser.SkipUnwatchedImportFromTrakt)
                            {
                                userData.Played = false;

                                _userDataManager.SaveUserData(
                                    user.Id,
                                    libraryMovie,
                                    userData,
                                    UserDataSaveReason.Import,
                                    cancellationToken);
                            }
                        }
                    }
                    else
                    {
                        // If the movie has not been played locally but is played on trakt.tv then add it to the unplayed list
                        if (movieWatched != null && traktUser.PostUnwatchedHistory)
                        {
                            unplayedMovies.Add(libraryMovie);
                        }
                    }
                }
            }
        }
        while (previousCount != 0);

        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send movies to mark collected
        await SendMovieCollectionUpdates(true, traktUser, collectedMovies, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send movies to mark watched
        await SendMoviePlaystateUpdates(true, traktUser, playedMovies, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send movies to mark unwatched
        await SendMoviePlaystateUpdates(false, traktUser, unplayedMovies, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);
    }

    private async Task SendMovieCollectionUpdates(
        bool collected,
        TraktUser traktUser,
        List<Movie> movies,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        if (movies.Count > 0)
        {
            _logger.LogInformation("Movies to {State} collection {Count}", collected ? "add to" : "remove from", movies.Count);
            try
            {
                List<TraktSyncResponse> dataContracts;
                var percentPerRequest = availablePercent / (movies.Count / 100.0);

                // Force update trakt.tv if we have more than 100 movies in the queue due to API
                var offset = 0;
                while (offset + 100 < movies.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var moviesToSend = movies.GetRange(offset, 100);
                    dataContracts = (await _traktApi.SendLibraryUpdateAsync(
                                moviesToSend,
                                traktUser,
                                collected ? EventType.Add : EventType.Remove,
                                cancellationToken)
                            .ConfigureAwait(false))
                        .ToList();

                    offset += 100;
                    currentProgress += percentPerRequest;
                    progress.Report(currentProgress);

                    LogTraktResponseDataContract(dataContracts, TraktItemType.movie);
                }

                dataContracts = (await _traktApi.SendLibraryUpdateAsync(
                            movies.GetRange(offset, movies.Count - offset),
                            traktUser,
                            collected ? EventType.Add : EventType.Remove,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToList();

                currentProgress += percentPerRequest;
                progress.Report(currentProgress);

                LogTraktResponseDataContract(dataContracts, TraktItemType.movie);
            }
            catch (ArgumentNullException argNullEx)
            {
                _logger.LogError(argNullEx, "ArgumentNullException handled sending movies to trakt.tv");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception handled sending movies to trakt.tv");
            }
        }
    }

    private async Task SendMoviePlaystateUpdates(
        bool seen,
        TraktUser traktUser,
        List<Movie> movies,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        if (movies.Count > 0)
        {
            _logger.LogInformation("Movies to set {State}watched: {Count}", seen ? string.Empty : "un", movies.Count);
            try
            {
                List<TraktSyncResponse> dataContracts;
                var percentPerRequest = availablePercent / (movies.Count / 100.0);

                // Force update trakt.tv if we have more than 100 movies in the queue due to API
                var offset = 0;
                while (offset + 100 < movies.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var moviesToSend = movies.GetRange(offset, 100);
                    dataContracts = await _traktApi.SendMoviePlaystateUpdates(
                            moviesToSend,
                            traktUser,
                            seen,
                            cancellationToken)
                        .ConfigureAwait(false);

                    offset += 100;
                    currentProgress += percentPerRequest;
                    progress.Report(currentProgress);

                    LogTraktResponseDataContract(dataContracts, TraktItemType.episode);
                }

                dataContracts = await _traktApi.SendMoviePlaystateUpdates(
                        movies.GetRange(offset, movies.Count - offset),
                        traktUser,
                        seen,
                        cancellationToken)
                    .ConfigureAwait(false);

                currentProgress += percentPerRequest;
                progress.Report(currentProgress);

                LogTraktResponseDataContract(dataContracts, TraktItemType.episode);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error updating movie play states");
            }
        }
    }

    private async Task SyncShows(
        Jellyfin.Data.Entities.User user,
        TraktUser traktUser,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        List<Api.DataContracts.Users.Watched.TraktShowWatched> traktWatchedShows = new List<Api.DataContracts.Users.Watched.TraktShowWatched>();
        List<Api.DataContracts.Users.Collection.TraktShowCollected> traktCollectedShows = new List<Api.DataContracts.Users.Collection.TraktShowCollected>();

        try
        {
            /*
            * In order to sync watched status to trakt.tv we need to know what's been watched on trakt.tv already. This
            * will stop us from endlessly incrementing the watched values on the site.
            */
            if (traktUser.PostWatchedHistory || traktUser.PostUnwatchedHistory)
            {
                traktWatchedShows.AddRange(await _traktApi.SendGetWatchedShowsRequest(traktUser).ConfigureAwait(false));
            }

            if (traktUser.SynchronizeCollections)
            {
                traktCollectedShows.AddRange(await _traktApi.SendGetCollectedShowsRequest(traktUser).ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handled in SyncShows");
            throw;
        }

        var baseQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            IsVirtualItem = false,
            OrderBy = new[] { (ItemSortBy.SeriesSortName, SortOrder.Ascending) }
        };

        // Purely for progress reporting
        availablePercent /= 4;

        var collectedEpisodes = new List<Episode>();
        var playedEpisodes = new List<Episode>();
        var unplayedEpisodes = new List<Episode>();

        const int Limit = 100;
        int offset = 0, previousCount;

        do
        {
            baseQuery.Limit = Limit;
            baseQuery.StartIndex = offset;

            var items = _libraryManager.GetItemList(baseQuery);
            previousCount = items.Count;
            offset += Limit;
            var episodeItems = items.OfType<Episode>().Where(x => _traktApi.CanSync(x, traktUser));

            if (episodeItems != null)
            {
                foreach (var episode in episodeItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var userData = _userDataManager.GetUserData(user.Id, episode);
                    var isPlayedTraktTv = false;
                    var traktWatchedShow = Extensions.FindMatch(episode.Series, traktWatchedShows);

                    if (traktUser.PostSetUnwatched || traktUser.PostSetWatched)
                    {
                        if (traktWatchedShow?.Seasons != null && traktWatchedShow.Seasons.Count > 0)
                        {
                            isPlayedTraktTv = traktWatchedShow.Seasons.Any(
                                season => season.Number == episode.GetSeasonNumber()
                                    && season.Episodes != null
                                    && season.Episodes.Any(e => episode.ContainsEpisodeNumber(e.Number)
                                        && e.Plays > 0));
                        }

                        // If the show has been played locally and is unplayed on trakt.tv then add it to the list
                        if (traktUser.PostWatchedHistory && userData != null && userData.Played && !isPlayedTraktTv)
                        {
                            playedEpisodes.Add(episode);
                        }
                        else if (traktUser.PostUnwatchedHistory && userData != null && !userData.Played && isPlayedTraktTv)
                        {
                            // If the show has not been played locally but is played on trakt.tv then add it to the unplayed list
                            unplayedEpisodes.Add(episode);
                        }
                    }

                    if (traktUser.SynchronizeCollections)
                    {
                        var traktCollectedShow = Extensions.FindMatch(episode.Series, traktCollectedShows);
                        if (traktCollectedShow?.Seasons == null
                            || traktCollectedShow.Seasons.All(season => season.Number != episode.GetSeasonNumber())
                            || traktCollectedShow.Seasons.First(season => season.Number == episode.GetSeasonNumber())
                                .Episodes.All(e => e.Number != episode.IndexNumber))
                        {
                            collectedEpisodes.Add(episode);
                        }
                    }
                }
            }
        }
        while (previousCount != 0);

        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send episodes to mark collected
        await SendEpisodeCollectionUpdates(true, traktUser, collectedEpisodes, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send episodes to mark watched
        await SendEpisodePlaystateUpdates(true, traktUser, playedEpisodes, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);

        // Send episodes to mark unwatched
        await SendEpisodePlaystateUpdates(false, traktUser, unplayedEpisodes, progress, currentProgress, availablePercent, cancellationToken).ConfigureAwait(false);
        currentProgress += availablePercent;
        progress.Report(currentProgress);
    }

    private async Task SendEpisodeCollectionUpdates(
        bool collected,
        TraktUser traktUser,
        List<Episode> episodes,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        if (episodes.Count > 0)
        {
            _logger.LogInformation("Episodes to {State} collection {Count}", collected ? "add to" : "remove from", episodes.Count);
            try
            {
                List<TraktSyncResponse> dataContracts;
                var percentPerRequest = availablePercent / (episodes.Count / 100.0);

                // Force update trakt.tv if we have more than 100 movies in the queue due to API
                var offset = 0;
                while (offset + 100 < episodes.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var episodesToSend = episodes.GetRange(offset, 100);
                    dataContracts = (await _traktApi.SendLibraryUpdateAsync(
                                episodesToSend,
                                traktUser,
                                collected ? EventType.Add : EventType.Remove,
                                cancellationToken)
                            .ConfigureAwait(false))
                        .ToList();

                    offset += 100;
                    currentProgress += percentPerRequest;
                    progress.Report(currentProgress);

                    LogTraktResponseDataContract(dataContracts, TraktItemType.episode);
                }

                dataContracts = (await _traktApi.SendLibraryUpdateAsync(
                            episodes.GetRange(offset, episodes.Count - offset),
                            traktUser,
                            collected ? EventType.Add : EventType.Remove,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToList();

                currentProgress += percentPerRequest;
                progress.Report(currentProgress);

                LogTraktResponseDataContract(dataContracts, TraktItemType.episode);
            }
            catch (ArgumentNullException argNullEx)
            {
                _logger.LogError(argNullEx, "ArgumentNullException handled sending episodes to trakt.tv");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception handled sending episodes to trakt.tv");
            }
        }
    }

    private async Task SendEpisodePlaystateUpdates(
        bool seen,
        TraktUser traktUser,
        List<Episode> episodes,
        IProgress<double> progress,
        double currentProgress,
        double availablePercent,
        CancellationToken cancellationToken)
    {
        if (episodes.Count > 0)
        {
            _logger.LogInformation("Episodes to set {State}watched: {Count}", seen ? string.Empty : "un", episodes.Count);
            try
            {
                List<TraktSyncResponse> dataContracts;
                var percentPerRequest = availablePercent / (episodes.Count / 100.0);

                // Force update trakt.tv if we have more than 100 movies in the queue due to API
                var offset = 0;
                while (offset + 100 < episodes.Count)
                {
                    var episodesToSend = episodes.GetRange(offset, 100);
                    cancellationToken.ThrowIfCancellationRequested();
                    dataContracts = await _traktApi.SendEpisodePlaystateUpdates(
                            episodesToSend,
                            traktUser,
                            seen,
                            cancellationToken)
                        .ConfigureAwait(false);

                    offset += 100;
                    currentProgress += percentPerRequest;
                    progress.Report(currentProgress);

                    LogTraktResponseDataContract(dataContracts, TraktItemType.episode);
                }

                dataContracts = (await _traktApi.SendEpisodePlaystateUpdates(
                            episodes.GetRange(offset, episodes.Count - offset),
                            traktUser,
                            seen,
                            cancellationToken)
                        .ConfigureAwait(false))
                    .ToList();

                currentProgress += percentPerRequest;
                progress.Report(currentProgress);

                LogTraktResponseDataContract(dataContracts, TraktItemType.episode);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error updating episode play states");
            }
        }
    }

    private void LogTraktResponseDataContract(IReadOnlyCollection<TraktSyncResponse> dataContracts, TraktItemType type)
    {
        if (dataContracts.Count != 0)
        {
            foreach (var dataContract in dataContracts)
            {
                if (type is TraktItemType.movie)
                {
                    if (dataContract.Added?.Movies > 0)
                    {
                        _logger.LogDebug("Added movies: {Count}", dataContract.Added.Movies);
                    }

                    if (dataContract.Updated?.Movies > 0)
                    {
                        _logger.LogDebug("Updated movies: {Count}", dataContract.Updated.Movies);
                    }

                    if (dataContract.Deleted?.Movies > 0)
                    {
                        _logger.LogDebug("Removed movies: {Count}", dataContract.Deleted.Movies);
                    }

                    if (dataContract.NotFound is not null)
                    {
                        foreach (var traktMovie in dataContract.NotFound.Movies)
                        {
                            _logger.LogError("Movie not found on trakt.tv: {@TraktMovie}", traktMovie);
                        }
                    }
                }

                if (type is TraktItemType.episode)
                {
                    if (dataContract.Added?.Episodes > 0)
                    {
                        _logger.LogDebug("Added episodes: {Count}", dataContract.Added.Episodes);
                    }

                    if (dataContract.Updated?.Episodes > 0)
                    {
                        _logger.LogDebug("Updated episodes: {Count}", dataContract.Updated.Episodes);
                    }

                    if (dataContract.Deleted?.Episodes > 0)
                    {
                        _logger.LogDebug("Removed episodes: {Count}", dataContract.Deleted.Episodes);
                    }

                    if (dataContract.NotFound is not null)
                    {
                        foreach (var traktEpisode in dataContract.NotFound.Episodes)
                        {
                            _logger.LogError("Episode not found on trakt.tv: {@TraktEpisode}", traktEpisode);
                        }
                    }
                }
            }
        }
    }
}
