#pragma warning disable CA1002

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Trakt.Api.DataContracts;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.Scrobble;
using Trakt.Api.DataContracts.Sync;
using Trakt.Api.DataContracts.Sync.Collection;
using Trakt.Api.DataContracts.Sync.Ratings;
using Trakt.Api.DataContracts.Sync.Watched;
using Trakt.Helpers;
using Trakt.Model;
using TraktEpisodeCollected = Trakt.Api.DataContracts.Sync.Collection.TraktEpisodeCollected;
using TraktMovieCollected = Trakt.Api.DataContracts.Sync.Collection.TraktMovieCollected;
using TraktShowCollected = Trakt.Api.DataContracts.Sync.Collection.TraktShowCollected;

namespace Trakt.Api;

/// <summary>
///
/// </summary>
public class TraktApi
{
    private static readonly SemaphoreSlim _traktResourcePool = new SemaphoreSlim(1, 1);

    private readonly ILogger<TraktApi> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerApplicationHost _appHost;
    private readonly IUserDataManager _userDataManager;
    private readonly IFileSystem _fileSystem;
    private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

    public TraktApi(
        ILogger<TraktApi> logger,
        IHttpClientFactory httpClientFactory,
        IServerApplicationHost appHost,
        IUserDataManager userDataManager,
        IFileSystem fileSystem)
    {
        _httpClientFactory = httpClientFactory;
        _appHost = appHost;
        _userDataManager = userDataManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether it's possible/allowed to sync a <see cref="BaseItem"/> for a <see cref="TraktUser"/>.
    /// </summary>
    /// <param name="item">
    /// Item to check.
    /// </param>
    /// <param name="traktUser">
    /// The trakt user to check for.
    /// </param>
    /// <returns>
    /// <see cref="bool"/> indicates if it's possible/allowed to sync this item.
    /// </returns>
    public bool CanSync(BaseItem item, TraktUser traktUser)
    {
        if (item.Path == null || item.LocationType == LocationType.Virtual)
        {
            return false;
        }

        if (traktUser.LocationsExcluded != null && traktUser.LocationsExcluded.Any(s => _fileSystem.ContainsSubPath(s, item.Path)))
        {
            return false;
        }

        if (item is Movie movie)
        {
            return !string.IsNullOrEmpty(movie.GetProviderId(MetadataProvider.Imdb)) ||
                   !string.IsNullOrEmpty(movie.GetProviderId(MetadataProvider.Tmdb));
        }

        if (item is Episode episode
            && episode.Series != null
            && !episode.IsMissingEpisode
            && (episode.IndexNumber.HasValue
                || !string.IsNullOrEmpty(episode.GetProviderId(MetadataProvider.Imdb))
                || !string.IsNullOrEmpty(episode.GetProviderId(MetadataProvider.Tmdb))
                || !string.IsNullOrEmpty(episode.GetProviderId(MetadataProvider.Tvdb))
                || !string.IsNullOrEmpty(episode.GetProviderId(MetadataProvider.TvRage))
            ))
        {
            var series = episode.Series;

            return !string.IsNullOrEmpty(series.GetProviderId(MetadataProvider.Imdb))
                   || !string.IsNullOrEmpty(series.GetProviderId(MetadataProvider.Tmdb))
                   || !string.IsNullOrEmpty(series.GetProviderId(MetadataProvider.TvRage))
                   || !string.IsNullOrEmpty(series.GetProviderId(MetadataProvider.Tvdb));
        }

        return false;
    }

    /// <summary>
    /// Report to trakt.tv that a movie is being watched, or has been watched.
    /// </summary>
    /// <param name="movie">The movie being watched/scrobbled</param>
    /// <param name="mediaStatus">MediaStatus enum dictating whether item is being watched or scrobbled</param>
    /// <param name="traktUser">The user that watching the current movie</param>
    /// <param name="progressPercent"></param>
    /// <returns>A standard TraktResponse Data Contract</returns>
    public async Task<TraktScrobbleResponse> SendMovieStatusUpdateAsync(Movie movie, MediaStatus mediaStatus, TraktUser traktUser, float progressPercent)
    {
        var movieData = new TraktScrobbleMovie
        {
            AppDate = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            AppVersion = _appHost.ApplicationVersionString,
            Progress = progressPercent,
            Movie = new TraktMovie
            {
                Title = movie.Name,
                Year = movie.ProductionYear,
                Ids = GetTraktIMDBTMDBIds<Movie, TraktMovieId>(movie)
            }
        };

        string url;
        switch (mediaStatus)
        {
            case MediaStatus.Watching:
                url = TraktUris.ScrobbleStart;
                break;
            case MediaStatus.Paused:
                url = TraktUris.ScrobblePause;
                break;
            default:
                url = TraktUris.ScrobbleStop;
                break;
        }

        using (var response = await PostToTrakt(url, movieData, traktUser, CancellationToken.None).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<TraktScrobbleResponse>(response, _jsonOptions).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reports to trakt.tv that an episode is being watched. Or that Episode(s) have been watched.
    /// </summary>
    /// <param name="episode">The episode being watched</param>
    /// <param name="status">Enum indicating whether an episode is being watched or scrobbled</param>
    /// <param name="traktUser">The user that's watching the episode</param>
    /// <param name="progressPercent"></param>
    /// <returns>A List of standard TraktResponse Data Contracts</returns>
    public async Task<List<TraktScrobbleResponse>> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus status, TraktUser traktUser, float progressPercent)
    {
        var episodeDatas = new List<TraktScrobbleEpisode>();

        var indexNumber = 0;
        var finalNumber = 0;
        if (episode.IndexNumber.HasValue)
        {
            indexNumber = episode.IndexNumber.Value;
            finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;
        }

        var number = indexNumber;
        var firstPass = true;
        do
        {
            var scrobbleEpisode = new TraktScrobbleEpisode
            {
                AppDate = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                AppVersion = _appHost.ApplicationVersionString,
                Progress = progressPercent,
                Episode = new TraktEpisode
                {
                    Season = episode.GetSeasonNumber()
                },
                Show = new TraktShow
                {
                    Title = episode.Series.Name,
                    Year = episode.Series.ProductionYear,
                    Ids = GetTraktTvIds<Series, TraktShowId>(episode.Series)
                }
            };
            if (episode.IndexNumber.HasValue)
            {
                scrobbleEpisode.Episode.Number = number;
            }

            // provider IDs in multi-episode file will be for the first episode only
            if (firstPass)
            {
                // output provider IDs for first episode
                scrobbleEpisode.Episode.Ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode);
                firstPass = false;
            }

            episodeDatas.Add(scrobbleEpisode);

            number++;
        }
        while (episode.IndexNumber.HasValue && number <= finalNumber);

        string url;
        switch (status)
        {
            case MediaStatus.Watching:
                url = TraktUris.ScrobbleStart;
                break;
            case MediaStatus.Paused:
                url = TraktUris.ScrobblePause;
                break;
            default:
                url = TraktUris.ScrobbleStop;
                break;
        }

        var responses = new List<TraktScrobbleResponse>();
        foreach (var traktScrobbleEpisode in episodeDatas)
        {
            using (var response = await PostToTrakt(url, traktScrobbleEpisode, traktUser, CancellationToken.None).ConfigureAwait(false))
            {
                responses.Add(await JsonSerializer.DeserializeAsync<TraktScrobbleResponse>(response, _jsonOptions).ConfigureAwait(false));
            }
        }

        return responses;
    }

    /// <summary>
    /// Add or remove a list of movies to/from the users trakt.tv library
    /// </summary>
    /// <param name="movies">The movies to add</param>
    /// <param name="traktUser">The user who's library is being updated</param>
    /// <param name="eventType"></param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task{TraktResponseDataContract}.</returns>
    public async Task<IEnumerable<TraktSyncResponse>> SendLibraryUpdateAsync(
        IList<Movie> movies,
        TraktUser traktUser,
        EventType eventType,
        CancellationToken cancellationToken)
    {
        if (movies == null)
        {
            throw new ArgumentNullException(nameof(movies));
        }

        if (traktUser == null)
        {
            throw new ArgumentNullException(nameof(traktUser));
        }

        if (eventType == EventType.Update)
        {
            return null;
        }

        var moviesPayload = movies.Select(m =>
        {
            var audioStream = m.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);
            var traktMovieCollected = new TraktMovieCollected
            {
                CollectedAt = m.DateCreated.ToISO8601(),
                Title = m.Name,
                Year = m.ProductionYear,
                Ids = GetTraktIMDBTMDBIds<Movie, TraktMovieId>(m)
            };
            if (traktUser.ExportMediaInfo)
            {
                traktMovieCollected.AudioChannels = audioStream.GetAudioChannels();
                traktMovieCollected.Audio = audioStream.GetCodecRepresetation();
                traktMovieCollected.Resolution = m.GetDefaultVideoStream().GetResolution();
            }

            return traktMovieCollected;
        }).ToList();
        var url = eventType == EventType.Add ? TraktUris.SyncCollectionAdd : TraktUris.SyncCollectionRemove;

        var responses = new List<TraktSyncResponse>();
        var chunks = moviesPayload.ToChunks(100);
        foreach (var chunk in chunks)
        {
            var data = new TraktSyncCollected
            {
                Movies = chunk.ToList()
            };
            using (var response = await PostToTrakt(url, data, traktUser, cancellationToken).ConfigureAwait(false))
            {
                responses.Add(await JsonSerializer.DeserializeAsync<TraktSyncResponse>(response, _jsonOptions, cancellationToken).ConfigureAwait(false));
            }
        }

        return responses;
    }

    /// <summary>
    /// Add or remove a list of Episodes to/from the users trakt.tv library
    /// </summary>
    /// <param name="episodes">The episodes to add</param>
    /// <param name="traktUser">The user who's library is being updated</param>
    /// <param name="eventType"></param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task{TraktResponseDataContract}.</returns>
    public async Task<IEnumerable<TraktSyncResponse>> SendLibraryUpdateAsync(
        IReadOnlyList<Episode> episodes,
        TraktUser traktUser,
        EventType eventType,
        CancellationToken cancellationToken)
    {
        if (episodes == null)
        {
            throw new ArgumentNullException(nameof(episodes));
        }

        if (traktUser == null)
        {
            throw new ArgumentNullException(nameof(traktUser));
        }

        if (eventType == EventType.Update)
        {
            return null;
        }

        var responses = new List<TraktSyncResponse>();
        var chunks = episodes.ToChunks(100);
        foreach (var chunk in chunks)
        {
            responses.Add(await SendLibraryUpdateInternalAsync(chunk.ToList(), traktUser, eventType, cancellationToken).ConfigureAwait(false));
        }

        return responses;
    }

    private async Task<TraktSyncResponse> SendLibraryUpdateInternalAsync(
        IEnumerable<Episode> episodes,
        TraktUser traktUser,
        EventType eventType,
        CancellationToken cancellationToken)
    {
        var episodesPayload = new List<TraktEpisodeCollected>();
        var showPayload = new List<TraktShowCollected>();
        foreach (Episode episode in episodes)
        {
            var audioStream = episode.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);

            var syncShow = FindShow(showPayload, episode.Series);
            if (syncShow == null)
            {
                syncShow = new TraktShowCollected
                {
                    Ids = GetTraktTvIds<Series, TraktShowId>(episode.Series),
                    Seasons = new List<TraktSeasonCollected>()
                };

                showPayload.Add(syncShow);
            }

            var syncSeason =
                syncShow.Seasons.FirstOrDefault(ss => ss.Number == episode.GetSeasonNumber());
            if (syncSeason == null)
            {
                syncSeason = new TraktSeasonCollected
                {
                    Number = episode.GetSeasonNumber(),
                    Episodes = new List<TraktEpisodeCollected>()
                };

                syncShow.Seasons.Add(syncSeason);
            }

            var indexNumber = 0;
            var finalNumber = 0;
            if (episode.IndexNumber.HasValue)
            {
                indexNumber = episode.IndexNumber.Value;
                finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;
            }

            var number = indexNumber;
            var firstPass = true;
            do
            {
                var traktEpisodeCollected = new TraktEpisodeCollected
                {
                    CollectedAt = episode.DateCreated.ToISO8601()
                };

                if (episode.IndexNumber.HasValue)
                {
                    traktEpisodeCollected.Number = number;
                }

                if (firstPass)
                {
                    // Omit this from the rest because then we end up attaching the provider IDs of the first episode to the subsequent ones
                    traktEpisodeCollected.Ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode);
                    firstPass = false;
                }

                if (traktUser.ExportMediaInfo)
                {
                    // traktEpisodeCollected.Is3D = episode.Is3D;
                    traktEpisodeCollected.AudioChannels = audioStream.GetAudioChannels();
                    traktEpisodeCollected.Audio = audioStream.GetCodecRepresetation();
                    traktEpisodeCollected.Resolution = episode.GetDefaultVideoStream().GetResolution();
                }

                syncSeason.Episodes.Add(traktEpisodeCollected);

                number++;
            }
            while (episode.IndexNumber.HasValue && number <= finalNumber);
        }

        var data = new TraktSyncCollected
        {
            Episodes = episodesPayload.ToList(),
            Shows = showPayload.ToList()
        };

        var url = eventType == EventType.Add ? TraktUris.SyncCollectionAdd : TraktUris.SyncCollectionRemove;
        using (var response = await PostToTrakt(url, data, traktUser, cancellationToken).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<TraktSyncResponse>(response, _jsonOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Add or remove a Show(Series) to/from the users trakt.tv library
    /// </summary>
    /// <param name="show">The show to remove</param>
    /// <param name="traktUser">The user who's library is being updated</param>
    /// <param name="eventType"></param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task{TraktResponseDataContract}.</returns>
    public async Task<TraktSyncResponse> SendLibraryUpdateAsync(
        Series show,
        TraktUser traktUser,
        EventType eventType,
        CancellationToken cancellationToken)
    {
        if (show == null)
        {
            throw new ArgumentNullException(nameof(show));
        }

        if (traktUser == null)
        {
            throw new ArgumentNullException(nameof(traktUser));
        }

        if (eventType == EventType.Update)
        {
            return null;
        }

        var showPayload = new List<TraktShowCollected>
        {
            new TraktShowCollected
            {
                Title = show.Name,
                Year = show.ProductionYear,
                Ids = GetTraktTvIds<Series, TraktShowId>(show)
            }
        };

        var data = new TraktSyncCollected
        {
            Shows = showPayload.ToList()
        };

        var url = eventType == EventType.Add ? TraktUris.SyncCollectionAdd : TraktUris.SyncCollectionRemove;
        using (var response = await PostToTrakt(url, data, traktUser, cancellationToken).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<TraktSyncResponse>(response, _jsonOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rate an item
    /// </summary>
    /// <param name="item"></param>
    /// <param name="rating"></param>
    /// <param name="traktUser"></param>
    /// <returns></returns>
    public async Task<TraktSyncResponse> SendItemRating(BaseItem item, int rating, TraktUser traktUser)
    {
        object data = new { };
        if (item is Movie)
        {
            data = new
            {
                movies = new[]
                {
                    new TraktMovieRated
                    {
                        Title = item.Name,
                        Year = item.ProductionYear,
                        Ids = GetTraktIMDBTMDBIds<Movie, TraktMovieId>((Movie)item),
                        Rating = rating
                    }
                }
            };
        }
        else if (item is Episode episode)
        {
            var show = new TraktShowRated
            {
                Ids = GetTraktTvIds<Series, TraktShowId>(episode.Series),
                Seasons = new List<TraktSeasonRated>
                {
                    new TraktSeasonRated
                    {
                        Number = episode.GetSeasonNumber(),
                        Episodes = new List<TraktEpisodeRated>
                        {
                            new TraktEpisodeRated
                            {
                                Number = episode.IndexNumber,
                                Rating = rating,
                                Ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode)
                            }
                        }
                    }
                }
            };
            data = new
            {
                shows = new[]
                {
                    show
                }
            };
        }
        else // It's a Series
        {
            data = new
            {
                shows = new[]
                {
                    new TraktShowRated
                    {
                        Rating = rating,
                        Title = item.Name,
                        Year = item.ProductionYear,
                        Ids = GetTraktTvIds<Series, TraktShowId>((Series)item)
                    }
                }
            };
        }

        using (var response = await PostToTrakt(TraktUris.SyncRatingsAdd, data, traktUser).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<TraktSyncResponse>(response, _jsonOptions).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="traktUser"></param>
    /// <returns></returns>
    public async Task<List<TraktMovie>> SendMovieRecommendationsRequest(TraktUser traktUser)
    {
        using (var response = await GetFromTrakt(TraktUris.RecommendationsMovies, traktUser).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<List<TraktMovie>>(response, _jsonOptions).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="traktUser"></param>
    /// <returns></returns>
    public async Task<List<TraktShow>> SendShowRecommendationsRequest(TraktUser traktUser)
    {
        using (var response = await GetFromTrakt(TraktUris.RecommendationsShows, traktUser).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<List<TraktShow>>(response, _jsonOptions).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="traktUser"></param>
    /// <returns></returns>
    public async Task<List<DataContracts.Users.Watched.TraktMovieWatched>> SendGetAllWatchedMoviesRequest(TraktUser traktUser)
    {
        using (var response = await GetFromTrakt(TraktUris.WatchedMovies, traktUser).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<List<DataContracts.Users.Watched.TraktMovieWatched>>(response, _jsonOptions).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="traktUser"></param>
    /// <returns></returns>
    public async Task<List<DataContracts.Users.Watched.TraktShowWatched>> SendGetWatchedShowsRequest(TraktUser traktUser)
    {
        using (var response = await GetFromTrakt(TraktUris.WatchedShows, traktUser).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<List<DataContracts.Users.Watched.TraktShowWatched>>(response, _jsonOptions).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="traktUser"></param>
    /// <returns></returns>
    public async Task<List<DataContracts.Users.Collection.TraktMovieCollected>> SendGetAllCollectedMoviesRequest(TraktUser traktUser)
    {
        using (var response = await GetFromTrakt(TraktUris.CollectedMovies, traktUser).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<List<DataContracts.Users.Collection.TraktMovieCollected>>(response, _jsonOptions).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="traktUser"></param>
    /// <returns></returns>
    public async Task<List<DataContracts.Users.Collection.TraktShowCollected>> SendGetCollectedShowsRequest(TraktUser traktUser)
    {
        using (var response = await GetFromTrakt(TraktUris.CollectedShows, traktUser).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<List<DataContracts.Users.Collection.TraktShowCollected>>(response, _jsonOptions).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Send a list of movies to trakt.tv that have been marked watched or unwatched.
    /// </summary>
    /// <param name="movies">The list of movies to send</param>
    /// <param name="traktUser">The trakt user profile that is being updated</param>
    /// <param name="seen">True if movies are being marked seen, false otherwise</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns></returns>
    // TODO: netstandard2.1: use IAsyncEnumerable
    public async Task<List<TraktSyncResponse>> SendMoviePlaystateUpdates(List<Movie> movies, TraktUser traktUser, bool seen, CancellationToken cancellationToken)
    {
        if (movies == null)
        {
            throw new ArgumentNullException(nameof(movies));
        }

        if (traktUser == null)
        {
            throw new ArgumentNullException(nameof(traktUser));
        }

        var moviesPayload = movies.Select(m =>
        {
            var lastPlayedDate = seen
                ? _userDataManager.GetUserData(new Guid(traktUser.LinkedMbUserId), m).LastPlayedDate
                : null;
            return new TraktMovieWatched
            {
                Title = m.Name,
                Ids = GetTraktIMDBTMDBIds<Movie, TraktMovieId>(m),
                Year = m.ProductionYear,
                WatchedAt = lastPlayedDate?.ToISO8601()
            };
        }).ToList();
        var chunks = moviesPayload.ToChunks(100).ToList();
        var traktResponses = new List<TraktSyncResponse>();

        foreach (var chunk in chunks)
        {
            var data = new TraktSyncWatched
            {
                Movies = chunk.ToList()
            };
            var url = seen ? TraktUris.SyncWatchedHistoryAdd : TraktUris.SyncWatchedHistoryRemove;

            using (var response = await PostToTrakt(url, data, traktUser, cancellationToken).ConfigureAwait(false))
            {
                if (response != null)
                {
                    traktResponses.Add(await JsonSerializer.DeserializeAsync<TraktSyncResponse>(response, _jsonOptions, cancellationToken).ConfigureAwait(false));
                }
            }
        }

        return traktResponses;
    }

    /// <summary>
    /// Send a list of episodes to trakt.tv that have been marked watched or unwatched
    /// </summary>
    /// <param name="episodes">The list of episodes to send</param>
    /// <param name="traktUser">The trakt user profile that is being updated</param>
    /// <param name="seen">True if episodes are being marked seen, false otherwise</param>
    /// <param name="cancellationToken">The Cancellation Token</param>
    /// <returns></returns>
    public async Task<List<TraktSyncResponse>> SendEpisodePlaystateUpdates(List<Episode> episodes, TraktUser traktUser, bool seen, CancellationToken cancellationToken)
    {
        if (episodes == null)
        {
            throw new ArgumentNullException(nameof(episodes));
        }

        if (traktUser == null)
        {
            throw new ArgumentNullException(nameof(traktUser));
        }

        var chunks = episodes.ToChunks(100).ToList();
        var traktResponses = new List<TraktSyncResponse>();

        foreach (var chunk in chunks)
        {
            var response = await SendEpisodePlaystateUpdatesInternalAsync(chunk, traktUser, seen, cancellationToken).ConfigureAwait(false);

            if (response != null)
            {
                traktResponses.Add(response);
            }
        }

        return traktResponses;
    }

    private async Task<TraktSyncResponse> SendEpisodePlaystateUpdatesInternalAsync(IEnumerable<Episode> episodeChunk, TraktUser traktUser, bool seen, CancellationToken cancellationToken)
    {
        var data = new TraktSyncWatched { Episodes = new List<TraktEpisodeWatched>(), Shows = new List<TraktShowWatched>() };
        foreach (var episode in episodeChunk)
        {
            var lastPlayedDate = seen
                ? _userDataManager.GetUserData(new Guid(traktUser.LinkedMbUserId), episode)
                    .LastPlayedDate
                : null;

            var syncShow = FindShow(data.Shows, episode.Series);
            if (syncShow == null)
            {
                syncShow = new TraktShowWatched
                {
                    Ids = GetTraktTvIds<Series, TraktShowId>(episode.Series),
                    Seasons = new List<TraktSeasonWatched>()
                };
                data.Shows.Add(syncShow);
            }

            var syncSeason = syncShow.Seasons.FirstOrDefault(ss => ss.Number == episode.GetSeasonNumber());
            if (syncSeason == null)
            {
                syncSeason = new TraktSeasonWatched
                {
                    Number = episode.GetSeasonNumber(),
                    Episodes = new List<TraktEpisodeWatched>()
                };
                syncShow.Seasons.Add(syncSeason);
            }

            var indexNumber = 0;
            var finalNumber = 0;
            if (episode.IndexNumber.HasValue)
            {
                indexNumber = episode.IndexNumber.Value;
                finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;
            }

            var number = indexNumber;
            var firstPass = true;
            do
            {
                var watchedEpisode = new TraktEpisodeWatched
                {
                    WatchedAt = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : null
                };
                if (episode.IndexNumber.HasValue)
                {
                    watchedEpisode.Number = number;
                }

                // provider IDs in multi-episode file will be for the first episode only
                if (firstPass)
                {
                    // output provider IDs for first episode
                    watchedEpisode.Ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode);
                    firstPass = false;
                }

                syncSeason.Episodes.Add(watchedEpisode);

                number++;
            }
            while (episode.IndexNumber.HasValue && number <= finalNumber);
        }

        var url = seen ? TraktUris.SyncWatchedHistoryAdd : TraktUris.SyncWatchedHistoryRemove;

        using (var response = await PostToTrakt(url, data, traktUser, cancellationToken).ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync<TraktSyncResponse>(response, _jsonOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> AuthorizeDevice(TraktUser traktUser)
    {
        var deviceCodeRequest = new
        {
            client_id = TraktUris.ClientId
        };

        TraktDeviceCode deviceCode;
        using (var response = await PostToTrakt(TraktUris.DeviceCode, deviceCodeRequest, null).ConfigureAwait(false))
        {
            deviceCode = await JsonSerializer.DeserializeAsync<TraktDeviceCode>(response, _jsonOptions).ConfigureAwait(false);
        }

        // Start polling in the background
        Plugin.Instance.PollingTasks[traktUser.LinkedMbUserId] = Task.Run(() => PollForAccessToken(deviceCode, traktUser));

        return deviceCode.UserCode;
    }

    public async Task<bool> PollForAccessToken(TraktDeviceCode deviceCode, TraktUser traktUser)
    {
        var deviceAccessTokenRequest = new
        {
            code = deviceCode.DeviceCode,
            client_id = TraktUris.ClientId,
            client_secret = TraktUris.ClientSecret
        };

        var pollingInterval = deviceCode.Interval;
        var expiresAt = DateTime.UtcNow.AddSeconds(deviceCode.ExpiresIn);
        _logger.LogInformation("Polling for access token every {PollingInterval}s. Expires at {ExpiresAt} UTC.", pollingInterval, expiresAt);
        while (DateTime.UtcNow < expiresAt)
        {
            using (var response = await PostToTrakt(TraktUris.DeviceToken, deviceAccessTokenRequest).ConfigureAwait(false))
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.BadRequest:
                        // Pending - waiting for the user to authorize your app
                        break;
                    case HttpStatusCode.NotFound:
                        _logger.LogError("Not Found - invalid device_code");
                        break;
                    case HttpStatusCode.Conflict:
                        _logger.LogWarning("Already Used - user already approved this code");
                        return false;
                    case HttpStatusCode.Gone:
                        _logger.LogError("Expired - the tokens have expired, restart the process");
                        break;
                    case (HttpStatusCode)418:
                        _logger.LogInformation("Denied - user explicitly denied this code");
                        return false;
                    case (HttpStatusCode)429:
                        _logger.LogWarning("Polling too quickly. Slowing down");
                        pollingInterval += 1;
                        break;
                    case HttpStatusCode.OK:
                        _logger.LogInformation("Device successfully authorized");

                        var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        var userAccessToken = await JsonSerializer.DeserializeAsync<TraktUserAccessToken>(stream, _jsonOptions).ConfigureAwait(false);
                        if (userAccessToken != null)
                        {
                            traktUser.AccessToken = userAccessToken.AccessToken;
                            traktUser.RefreshToken = userAccessToken.RefreshToken;
                            traktUser.AccessTokenExpiration = DateTime.Now.AddSeconds(userAccessToken.ExpirationWithBuffer);
                            Plugin.Instance.SaveConfiguration();
                            return true;
                        }

                        break;
                }
            }

            await Task.Delay(pollingInterval * 1000).ConfigureAwait(false);
        }

        return false;
    }

    public async Task RefreshUserAccessToken(TraktUser traktUser)
    {
        if (string.IsNullOrWhiteSpace(traktUser.RefreshToken))
        {
            _logger.LogError("Tried to reauthenticate with Trakt, but no refreshToken was available");
            return;
        }

        var data = new TraktUserRefreshTokenRequest
        {
            ClientId = TraktUris.ClientId,
            ClientSecret = TraktUris.ClientSecret,
            RedirectUri = "urn:ietf:wg:oauth:2.0:oob",
            RefreshToken = traktUser.RefreshToken,
            GrantType = "refresh_token"
        };

        TraktUserAccessToken userAccessToken;
        try
        {
            using (var response = await PostToTrakt(TraktUris.AccessToken, data).ConfigureAwait(false))
            {
#pragma warning disable CA2007
                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#pragma warning restore CA2007
                userAccessToken = await JsonSerializer.DeserializeAsync<TraktUserAccessToken>(stream, _jsonOptions).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "An error occurred during token refresh");
            return;
        }

        if (userAccessToken != null)
        {
            traktUser.AccessToken = userAccessToken.AccessToken;
            traktUser.RefreshToken = userAccessToken.RefreshToken;
            traktUser.AccessTokenExpiration = DateTime.Now.AddSeconds(userAccessToken.ExpirationWithBuffer);
            Plugin.Instance.SaveConfiguration();
            _logger.LogInformation("Successfully refreshed the access token for user {UserId}", traktUser.LinkedMbUserId);
        }
    }

    private Task<Stream> GetFromTrakt(string url, TraktUser traktUser)
    {
        return GetFromTrakt(url, traktUser, CancellationToken.None);
    }

    private async Task<Stream> GetFromTrakt(string url, TraktUser traktUser, CancellationToken cancellationToken)
    {
        var httpClient = GetHttpClient();

        if (traktUser != null)
        {
            await SetRequestHeaders(httpClient, traktUser).ConfigureAwait(false);
        }

        await _traktResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await RetryHttpRequest(async () => await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _traktResourcePool.Release();
        }
    }

    private async Task<HttpResponseMessage> PostToTrakt(string url, object data)
    {
        var httpClient = GetHttpClient();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
        var content = new ByteArrayContent(bytes);
        content.Headers.Add(HeaderNames.ContentType, MediaTypeNames.Application.Json);

        await _traktResourcePool.WaitAsync().ConfigureAwait(false);

        try
        {
            return await httpClient.PostAsync(url, content).ConfigureAwait(false);
        }
        finally
        {
            _traktResourcePool.Release();
        }
    }

    private Task<Stream> PostToTrakt(string url, object data, TraktUser traktUser)
    {
        return PostToTrakt(url, data, traktUser, CancellationToken.None);
    }

    /// <summary>
    ///     Posts data to url, authenticating with <see cref="TraktUser"/>.
    /// </summary>
    /// <param name="traktUser">If null, authentication headers not added.</param>
    private async Task<Stream> PostToTrakt(
        string url,
        object data,
        TraktUser traktUser,
        CancellationToken cancellationToken)
    {
        if (traktUser != null && traktUser.ExtraLogging)
        {
            _logger.LogDebug("{@JsonData}", data);
        }

        var httpClient = GetHttpClient();

        if (traktUser != null)
        {
            await SetRequestHeaders(httpClient, traktUser).ConfigureAwait(false);
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
        var content = new ByteArrayContent(bytes);
        content.Headers.Add(HeaderNames.ContentType, MediaTypeNames.Application.Json);

        await _traktResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await RetryHttpRequest(async () => await httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _traktResourcePool.Release();
        }
    }

    private async Task<HttpResponseMessage> RetryHttpRequest(Func<Task<HttpResponseMessage>> function)
    {
        HttpResponseMessage response = null;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                response = await function().ConfigureAwait(false);
                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var delay = response.Headers.RetryAfter.Delta ?? TimeSpan.FromSeconds(1);
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                else
                {
                    break;
                }
            }
            catch (Exception)
            {
            }
        }

        return response;
    }

    private HttpClient GetHttpClient()
    {
        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        client.DefaultRequestHeaders.Add("trakt-api-version", "2");
        client.DefaultRequestHeaders.Add("trakt-api-key", TraktUris.ClientId);
        return client;
    }

    private async Task SetRequestHeaders(HttpClient httpClient, TraktUser traktUser)
    {
        if (DateTimeOffset.Now > traktUser.AccessTokenExpiration)
        {
            traktUser.AccessToken = string.Empty;
            await RefreshUserAccessToken(traktUser).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(traktUser.AccessToken))
        {
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + traktUser.AccessToken);
        }
    }

    private static TReturn GetTraktIMDBTMDBIds<TInput, TReturn>(TInput mediaObject)
        where TInput : IHasProviderIds
        where TReturn : TraktIMDBandTMDBId, new()
    {
        return new TReturn
        {
            Imdb = mediaObject.GetProviderId(MetadataProvider.Imdb),
            Tmdb = mediaObject.GetProviderId(MetadataProvider.Tmdb).ConvertToInt()
        };
    }

    private static TReturn GetTraktTvIds<TInput, TReturn>(TInput mediaObject)
        where TInput : IHasProviderIds
        where TReturn : TraktTVId, new()
    {
        TReturn retval = GetTraktIMDBTMDBIds<TInput, TReturn>(mediaObject);
        retval.Tvdb = mediaObject.GetProviderId(MetadataProvider.Tvdb).ConvertToInt();
        retval.Tvrage = mediaObject.GetProviderId(MetadataProvider.TvRage).ConvertToInt();
        return retval;
    }

    private static TTraktShow FindShow<TTraktShow>(List<TTraktShow> shows, Series series)
        where TTraktShow : TraktShow
    {
        return shows.FirstOrDefault(
            sre => sre.Ids != null && sre.Ids.Imdb == series.GetProviderId(MetadataProvider.Imdb) && sre.Ids.Tmdb == series.GetProviderId(MetadataProvider.Tmdb).ConvertToInt() && sre.Ids.Tvdb == series.GetProviderId(MetadataProvider.Tvdb).ConvertToInt() && sre.Ids.Tvrage == series.GetProviderId(MetadataProvider.TvRage).ConvertToInt());
    }
}
