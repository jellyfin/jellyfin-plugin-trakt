using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Trakt.Api.DataContracts;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.Scrobble;
using Trakt.Api.DataContracts.Sync;
using Trakt.Api.DataContracts.Sync.Collection;
using Trakt.Api.DataContracts.Sync.Ratings;
using Trakt.Api.DataContracts.Sync.Watched;
using Trakt.Model;
using Trakt.Model.Enums;
using TraktEpisodeCollected = Trakt.Api.DataContracts.Sync.Collection.TraktEpisodeCollected;
using TraktMovieCollected = Trakt.Api.DataContracts.Sync.Collection.TraktMovieCollected;
using TraktShowCollected = Trakt.Api.DataContracts.Sync.Collection.TraktShowCollected;

namespace Trakt.Api
{
    /// <summary>
    /// Trakt.tv API client class.
    /// </summary>
    public class TraktApi
    {
        private static readonly SemaphoreSlim _traktResourcePool = new SemaphoreSlim(1, 1);

        private readonly ILogger<TraktApi> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServerApplicationHost _appHost;
        private readonly IUserDataManager _userDataManager;
        private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraktApi"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/>.</param>
        /// <param name="appHost">The <see cref="IServerApplicationHost"/>.</param>
        /// <param name="userDataManager">The <see cref="IUserDataManager"/>.</param>
        public TraktApi(
            ILogger<TraktApi> logger,
            IHttpClientFactory httpClientFactory,
            IServerApplicationHost appHost,
            IUserDataManager userDataManager)
        {
            _httpClientFactory = httpClientFactory;
            _appHost = appHost;
            _userDataManager = userDataManager;
            _logger = logger;
        }

        /// <summary>
        /// Checks whether it's possible/allowed to sync a <see cref="BaseItem"/> for a <see cref="TraktUser"/>.
        /// </summary>
        /// <param name="item">Item to check.</param>
        /// <param name="traktUser">The trakt.tv user to check for.</param>
        /// <returns><see cref="bool"/> indicating if it's possible/allowed to sync this item.</returns>
        public bool CanSync(BaseItem item, TraktUser traktUser)
        {
            if (item.Path == null || item.LocationType == LocationType.Virtual)
            {
                return false;
            }

            if (traktUser.LocationsExcluded != null
                && traktUser.LocationsExcluded.Any(directory => item.Path.Contains(directory, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (item is Movie movie)
            {
                return movie.HasProviderId(MetadataProvider.Imdb)
                    || movie.HasProviderId(MetadataProvider.Tmdb);
            }

            if (item is Episode episode
                && episode.Series != null
                && !episode.IsMissingEpisode
                && (episode.IndexNumber.HasValue
                    || HasAnyProviderTvIds(episode)
                    ))
            {
                var series = episode.Series;

                return HasAnyProviderTvIds(series);
            }

            return false;
        }

        /// <summary>
        /// Report to trakt.tv that a movie is being watched, or has been watched.
        /// </summary>
        /// <param name="movie">The movie being watched/scrobbled.</param>
        /// <param name="mediaStatus">MediaStatus enum dictating whether item is being watched or scrobbled.</param>
        /// <param name="traktUser">The user that watching the current movie.</param>
        /// <param name="progressPercent">The progress percentage.</param>
        /// <returns>A standard TraktResponse Data Contract.</returns>
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

            return await PostToTrakt<TraktScrobbleResponse>(url, movieData, traktUser, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Reports to trakt.tv that an episode is being watched. Or that episode(s) have been watched.
        /// </summary>
        /// <param name="episode">The <see cref="Episode"/> being watched.</param>
        /// <param name="status">The <see cref="MediaStatus"/> indicating whether an episode is being watched or scrobbled.</param>
        /// <param name="traktUser">The <see cref="TraktUser"/> that's watching the episode.</param>
        /// <param name="progressPercent">The progress percentage.</param>
        /// <param name="useProviderIds"><see cref="bool"/> specifying if provider ids should be used for lookup or not.</param>
        /// <returns>Task{List{TraktScrobbleResponse}}.</returns>
        public async Task<List<TraktScrobbleResponse>> SendEpisodeStatusUpdateAsync(Episode episode, MediaStatus status, TraktUser traktUser, float progressPercent, bool useProviderIds = true)
        {
            var episodeDatas = new List<TraktScrobbleEpisode>();

            if (useProviderIds
                && HasAnyProviderTvIds(episode)
                && (!episode.IndexNumber.HasValue
                    || !episode.IndexNumberEnd.HasValue
                    || episode.IndexNumberEnd <= episode.IndexNumber))
            {
                episodeDatas.Add(new TraktScrobbleEpisode
                {
                    AppDate = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    AppVersion = _appHost.ApplicationVersionString,
                    Progress = progressPercent,
                    Episode = new TraktEpisode
                    {
                        Ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode)
                    }
                });
            }
            else if (episode.IndexNumber.HasValue)
            {
                var indexNumber = episode.IndexNumber.Value;
                var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;

                for (var number = indexNumber; number <= finalNumber; number++)
                {
                    episodeDatas.Add(new TraktScrobbleEpisode
                    {
                        AppDate = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        AppVersion = _appHost.ApplicationVersionString,
                        Progress = progressPercent,
                        Episode = new TraktEpisode
                        {
                            Season = episode.GetSeasonNumber(),
                            Number = number
                        },
                        Show = new TraktShow
                        {
                            Title = episode.Series.Name,
                            Year = episode.Series.ProductionYear,
                            Ids = GetTraktTvIds<Series, TraktShowId>(episode.Series)
                        }
                    });
                }
            }

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
                var response = await PostToTrakt<TraktScrobbleResponse>(url, traktScrobbleEpisode, traktUser, CancellationToken.None).ConfigureAwait(false);
                // Response can be empty if episode not found
                if (response is not null)
                {
                    responses.Add(response);
                }
                else if (useProviderIds && HasAnyProviderTvIds(episode))
                {
                    // Try scrobbling without IDs
                    _logger.LogDebug("Resend episode status update, without episode IDs");
                    responses = await SendEpisodeStatusUpdateAsync(episode, status, traktUser, progressPercent, false).ConfigureAwait(false);
                }
            }

            return responses;
        }

        /// <summary>
        /// Add or remove a list of movies to/from the user's trakt.tv library.
        /// </summary>
        /// <param name="movies">The movies to add.</param>
        /// <param name="traktUser">The <see cref="TraktUser"/> who's library is being updated.</param>
        /// <param name="eventType">The <see cref="EventType"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task{TraktResponseDataContract}.</returns>
        public async Task<IReadOnlyList<TraktSyncResponse>> SendLibraryUpdateAsync(
            ICollection<Movie> movies,
            TraktUser traktUser,
            EventType eventType,
            CancellationToken cancellationToken)
        {
            if (movies is null || movies.Count == 0)
            {
                throw new ArgumentNullException(nameof(movies));
            }

            if (traktUser == null)
            {
                throw new ArgumentNullException(nameof(traktUser));
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
            });

            var url = (eventType == EventType.Add || eventType == EventType.Update) ? TraktUris.SyncCollectionAdd : TraktUris.SyncCollectionRemove;
            var responses = new List<TraktSyncResponse>();
            var chunks = moviesPayload.ToChunks(100);
            foreach (var chunk in chunks)
            {
                var data = new TraktSyncCollected
                {
                    Movies = chunk.ToList()
                };

                var response = await PostToTrakt<TraktSyncResponse>(url, data, traktUser, cancellationToken).ConfigureAwait(false);
                responses.Add(response);
            }

            return responses;
        }

        /// <summary>
        /// Add or remove a list of episodes to/from the user's trakt.tv library.
        /// </summary>
        /// <param name="episodes">The episodes to add.</param>
        /// <param name="traktUser">The <see cref="TraktUser"/> who's library is being updated.</param>
        /// <param name="eventType">The <see cref="EventType"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task{IEnumerable{TraktSyncResponse}}.</returns>
        public async Task<IEnumerable<TraktSyncResponse>> SendLibraryUpdateAsync(
            ICollection<Episode> episodes,
            TraktUser traktUser,
            EventType eventType,
            CancellationToken cancellationToken)
        {
            if (episodes is null || episodes.Count == 0)
            {
                throw new ArgumentNullException(nameof(episodes));
            }

            if (traktUser == null)
            {
                throw new ArgumentNullException(nameof(traktUser));
            }

            var responses = new List<TraktSyncResponse>();
            var chunks = episodes.ToChunks(100);
            foreach (var chunk in chunks)
            {
                responses.Add(await SendLibraryUpdateInternalAsync(chunk, traktUser, eventType, cancellationToken).ConfigureAwait(false));
            }

            return responses;
        }

        private async Task<TraktSyncResponse> SendLibraryUpdateInternalAsync(
            IEnumerable<Episode> episodes,
            TraktUser traktUser,
            EventType eventType,
            CancellationToken cancellationToken,
            bool useProviderIDs = true)
        {
            var episodesPayload = new List<TraktEpisodeCollected>();
            var showPayload = new List<TraktShowCollected>();
            foreach (Episode episode in episodes)
            {
                var audioStream = episode.GetMediaStreams().FirstOrDefault(stream => stream.Type == MediaStreamType.Audio);

                if (useProviderIDs && HasAnyProviderTvIds(episode) &&
                    (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue ||
                     episode.IndexNumberEnd <= episode.IndexNumber))
                {
                    var traktEpisodeCollected = new TraktEpisodeCollected
                    {
                        CollectedAt = episode.DateCreated.ToISO8601(),
                        Ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode)
                    };
                    if (traktUser.ExportMediaInfo)
                    {
                        traktEpisodeCollected.AudioChannels = audioStream.GetAudioChannels();
                        traktEpisodeCollected.Audio = audioStream.GetCodecRepresetation();
                        traktEpisodeCollected.Resolution = episode.GetDefaultVideoStream().GetResolution();
                        traktEpisodeCollected.Is3D = episode.Is3D;
                        traktEpisodeCollected.Hdr = episode.GetDefaultVideoStream().GetHdr();
                    }

                    episodesPayload.Add(traktEpisodeCollected);
                }
                else if (episode.IndexNumber.HasValue)
                {
                    var indexNumber = episode.IndexNumber.Value;
                    var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;
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

                    var syncSeason = syncShow.Seasons.FirstOrDefault(season => season.Number == episode.GetSeasonNumber());
                    if (syncSeason == null)
                    {
                        syncSeason = new TraktSeasonCollected
                        {
                            Number = episode.GetSeasonNumber(),
                            Episodes = new List<TraktEpisodeCollected>()
                        };

                        syncShow.Seasons.Add(syncSeason);
                    }

                    for (var number = indexNumber; number <= finalNumber; number++)
                    {
                        var ids = new TraktEpisodeId();

                        if (number == indexNumber)
                        {
                            // Omit this from the rest because then we end up attaching the provider IDs of the first episode to the subsequent ones
                            ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode);
                        }

                        var traktEpisodeCollected = new TraktEpisodeCollected
                        {
                            Number = number,
                            CollectedAt = episode.DateCreated.ToISO8601(),
                            Ids = ids
                        };

                        if (traktUser.ExportMediaInfo)
                        {
                            var defaultVideoStream = episode.GetDefaultVideoStream();
                            traktEpisodeCollected.AudioChannels = audioStream.GetAudioChannels();
                            traktEpisodeCollected.Audio = audioStream.GetCodecRepresetation();
                            traktEpisodeCollected.Resolution = defaultVideoStream.GetResolution();
                            traktEpisodeCollected.Is3D = episode.Is3D;
                            traktEpisodeCollected.Hdr = defaultVideoStream.GetHdr();
                        }

                        syncSeason.Episodes.Add(traktEpisodeCollected);
                    }
                }
            }

            var data = new TraktSyncCollected
            {
                Episodes = episodesPayload,
                Shows = showPayload
            };

            var url = (eventType == EventType.Add || eventType == EventType.Update) ? TraktUris.SyncCollectionAdd : TraktUris.SyncCollectionRemove;
            var response = await PostToTrakt<TraktSyncResponse>(url, data, traktUser, cancellationToken).ConfigureAwait(false);
            if (useProviderIDs && response.NotFound.Episodes.Count > 0)
            {
                // Send subset of episodes back to trakt.tv to try without ids
                _logger.LogDebug("Resend episodes Library update, without episode IDs");
                await SendLibraryUpdateInternalAsync(FindNotFoundEpisodes(episodes, response), traktUser, eventType, cancellationToken, false).ConfigureAwait(false);
            }

            return response;
        }

        /// <summary>
        /// Add or remove a show/series to/from the user's trakt.tv library.
        /// </summary>
        /// <param name="show">The show to remove.</param>
        /// <param name="traktUser">The <see cref="TraktUser"/> who's library is being updated.</param>
        /// <param name="eventType">The <see cref="EventType"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task{TraktSyncResponse}.</returns>
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
                Shows = showPayload
            };

            var url = eventType == EventType.Add ? TraktUris.SyncCollectionAdd : TraktUris.SyncCollectionRemove;
            return await PostToTrakt<TraktSyncResponse>(url, data, traktUser, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Rate an item.
        /// </summary>
        /// <param name="item">The <see cref="BaseItem"/>.</param>
        /// <param name="rating">The rating.</param>
        /// <param name="traktUser">The <see cref="TraktUser"/> who's library is being updated.</param>
        /// <param name="useEpisodeProviderIDs">If provider ids should be used for episode syncing.</param>
        /// <returns>Task{TraktSyncResponse}.</returns>
        public async Task<TraktSyncResponse> SendItemRating(BaseItem item, int rating, TraktUser traktUser, bool useEpisodeProviderIDs = true)
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
                if (useEpisodeProviderIDs && HasAnyProviderTvIds(episode))
                {
                    data = new
                    {
                        episodes = new[]
                        {
                            new TraktEpisodeRated
                            {
                                Rating = rating,
                                Ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode)
                            }
                        }
                    };
                }
                else
                {
                    if (episode.IndexNumber.HasValue)
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
                                            Rating = rating
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
                }
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

            var response = await PostToTrakt<TraktSyncResponse>(TraktUris.SyncRatingsAdd, data, traktUser).ConfigureAwait(false);

            if (item is Episode && useEpisodeProviderIDs && response.NotFound.Episodes.Count > 0)
            {
                // Try sync without ids
                _logger.LogDebug("Resend episode rating, without episode IDs");
                return await SendItemRating(item, rating, traktUser, false).ConfigureAwait(false);
            }

            return response;
        }

        /// <summary>
        /// Get movie recommendations.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <returns>Task{List{TraktMovie}}.</returns>
        public async Task<List<TraktMovie>> SendMovieRecommendationsRequest(TraktUser traktUser)
        {
            return await GetFromTrakt<List<TraktMovie>>(TraktUris.RecommendationsMovies, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Get show recommendations.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <returns>Task{List{TraktShow}}.</returns>
        public async Task<List<TraktShow>> SendShowRecommendationsRequest(TraktUser traktUser)
        {
            return await GetFromTrakt<List<TraktShow>>(TraktUris.RecommendationsShows, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all watched movies.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <returns>Task{List{DataContracts.Users.Watched.TraktMovieWatched}}.</returns>
        public async Task<List<DataContracts.Users.Watched.TraktMovieWatched>> SendGetAllWatchedMoviesRequest(TraktUser traktUser)
        {
            return await GetFromTrakt<List<DataContracts.Users.Watched.TraktMovieWatched>>(TraktUris.WatchedMovies, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Get watched shows.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <returns>Task{List{DataContracts.Users.Watched.TraktShowWatched}}.</returns>
        public async Task<List<DataContracts.Users.Watched.TraktShowWatched>> SendGetWatchedShowsRequest(TraktUser traktUser)
        {
            return await GetFromTrakt<List<DataContracts.Users.Watched.TraktShowWatched>>(TraktUris.WatchedShows, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all paused movies.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <returns>Task{List{DataContracts.Users.Playback.TraktMoviePaused}}.</returns>
        public async Task<List<DataContracts.Users.Playback.TraktMoviePaused>> SendGetAllPausedMoviesRequest(TraktUser traktUser)
        {
            return await GetFromTrakt<List<DataContracts.Users.Playback.TraktMoviePaused>>(TraktUris.PausedMovies, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Get paused episodes.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <returns>Task{List{DataContracts.Users.Playback.TraktEpisodePaused}}.</returns>
        public async Task<List<DataContracts.Users.Playback.TraktEpisodePaused>> SendGetPausedEpisodesRequest(TraktUser traktUser)
        {
            return await GetFromTrakt<List<DataContracts.Users.Playback.TraktEpisodePaused>>(TraktUris.PausedEpisodes, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Get collected movies.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <returns>Task{List{DataContracts.Users.Collection.TraktMovieCollected}}.</returns>
        public async Task<List<DataContracts.Users.Collection.TraktMovieCollected>> SendGetAllCollectedMoviesRequest(TraktUser traktUser)
        {
            return await GetFromTrakt<List<DataContracts.Users.Collection.TraktMovieCollected>>(TraktUris.CollectedMovies, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Get collected shows.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <returns>Task{List{DataContracts.Users.Collection.TraktShowCollected}}.</returns>
        public async Task<List<DataContracts.Users.Collection.TraktShowCollected>> SendGetCollectedShowsRequest(TraktUser traktUser)
        {
            return await GetFromTrakt<List<DataContracts.Users.Collection.TraktShowCollected>>(TraktUris.CollectedShows, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Send a list of movies to trakt.tv that have been marked as watched or unwatched.
        /// </summary>
        /// <param name="movies">The list of movies to send.</param>
        /// <param name="traktUser">The trakt.tv user profile that is being updated.</param>
        /// <param name="seen">True if movies are being marked seen, false otherwise.</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <returns>Task{List{TraktSyncResponse}}.</returns>
        // TODO: netstandard2.1: use IAsyncEnumerable
        public async Task<List<TraktSyncResponse>> SendMoviePlaystateUpdates(
            ICollection<Movie> movies,
            TraktUser traktUser,
            bool seen,
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
            });
            var chunks = moviesPayload.ToChunks(100).ToList();
            var traktResponses = new List<TraktSyncResponse>();

            foreach (var chunk in chunks)
            {
                var data = new TraktSyncWatched
                {
                    Movies = chunk.ToList()
                };

                var url = seen ? TraktUris.SyncWatchedHistoryAdd : TraktUris.SyncWatchedHistoryRemove;
                var response = await PostToTrakt<TraktSyncResponse>(url, data, traktUser, cancellationToken).ConfigureAwait(false);
                if (response != null)
                {
                    traktResponses.Add(response);
                }
            }

            return traktResponses;
        }

        /// <summary>
        /// Send a list of episodes to trakt.tv that have been marked watched or unwatched.
        /// </summary>
        /// <param name="episodes">The list of episodes to send.</param>
        /// <param name="traktUser">The trakt.tv user profile that is being updated.</param>
        /// <param name="seen">True if episodes are being marked seen, false otherwise.</param>
        /// <param name="cancellationToken">The Cancellation Token.</param>
        /// <returns>Task{List{TraktSyncResponse}}.</returns>
        public async Task<List<TraktSyncResponse>> SendEpisodePlaystateUpdates(
            ICollection<Episode> episodes,
            TraktUser traktUser,
            bool seen,
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

            var chunks = episodes.ToChunks(100);
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

        private async Task<TraktSyncResponse> SendEpisodePlaystateUpdatesInternalAsync(
            IEnumerable<Episode> episodeChunk,
            TraktUser traktUser,
            bool seen,
            CancellationToken cancellationToken,
            bool useProviderIDs = true)
        {
            var data = new TraktSyncWatched
            {
                Episodes = new List<TraktEpisodeWatched>(),
                Shows = new List<TraktShowWatched>()
            };

            foreach (var episode in episodeChunk)
            {
                var lastPlayedDate = seen
                    ? _userDataManager.GetUserData(new Guid(traktUser.LinkedMbUserId), episode)
                        .LastPlayedDate
                    : null;

                if (useProviderIDs
                    && HasAnyProviderTvIds(episode)
                    && (!episode.IndexNumber.HasValue
                        || !episode.IndexNumberEnd.HasValue
                        || episode.IndexNumberEnd <= episode.IndexNumber))
                {
                    data.Episodes.Add(new TraktEpisodeWatched
                    {
                        Ids = GetTraktTvIds<Episode, TraktEpisodeId>(episode),
                        WatchedAt = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : null
                    });
                }
                else if (episode.IndexNumber != null)
                {
                    var indexNumber = episode.IndexNumber.Value;
                    var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;

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

                    for (var number = indexNumber; number <= finalNumber; number++)
                    {
                        syncSeason.Episodes.Add(new TraktEpisodeWatched
                        {
                            Number = number,
                            WatchedAt = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : null
                        });
                    }
                }
            }

            var url = seen ? TraktUris.SyncWatchedHistoryAdd : TraktUris.SyncWatchedHistoryRemove;

            var response = await PostToTrakt<TraktSyncResponse>(url, data, traktUser, cancellationToken).ConfigureAwait(false);

            if (useProviderIDs && response.NotFound.Episodes.Count > 0)
            {
                // Send subset of episodes back to trakt.tv to try without ids
                _logger.LogDebug("Resend episodes playstate update, without episode ids");
                await SendEpisodePlaystateUpdatesInternalAsync(FindNotFoundEpisodes(episodeChunk, response), traktUser, seen, cancellationToken, false).ConfigureAwait(false);
            }

            return response;
        }

        private List<Episode> FindNotFoundEpisodes(IEnumerable<Episode> episodeChunk, TraktSyncResponse traktSyncResponse)
        {
            // Episodes not found. If using ids, try again without them
            List<Episode> episodes = new List<Episode>();
            // Build a list of unfound episodes with ids
            foreach (TraktEpisode traktEpisode in traktSyncResponse.NotFound.Episodes.Where(episode => HasAnyProviderTvIds(episode.Ids)))
            {
                // Find matching episode in Jellyfin based on provider ids
                var notFoundEpisode = episodeChunk.FirstOrDefault(episode => episode.GetProviderId(MetadataProvider.Imdb) == traktEpisode.Ids.Imdb
                    || episode.GetProviderId(MetadataProvider.Tmdb) == traktEpisode.Ids.Tmdb?.ToString(CultureInfo.InvariantCulture)
                    || episode.GetProviderId(MetadataProvider.Tvdb) == traktEpisode.Ids.Tvdb?.ToString(CultureInfo.InvariantCulture)
                    || episode.GetProviderId(MetadataProvider.TvRage) == traktEpisode.Ids.Tvrage?.ToString(CultureInfo.InvariantCulture));

                if (notFoundEpisode != null)
                {
                    episodes.Add(notFoundEpisode);
                }
            }

            return episodes;
        }

        /// <summary>
        /// Authorizes a device for a <see cref="TraktUser"/>.
        /// </summary>
        /// <param name="traktUser">The authorizing <see cref="TraktUser"/>.</param>
        /// <returns>Task{string}.</returns>
        public async Task<string> AuthorizeDevice(TraktUser traktUser)
        {
            var deviceCodeRequest = new
            {
                client_id = TraktUris.ClientId
            };

            var deviceCode = await PostToTrakt<TraktDeviceCode>(TraktUris.DeviceCode, deviceCodeRequest, null).ConfigureAwait(false);

            // Start polling in the background
            Plugin.Instance.PollingTasks[traktUser.LinkedMbUserId] = Task.Run(() => PollForAccessToken(deviceCode, traktUser));

            return deviceCode.UserCode;
        }

        /// <summary>
        /// Deauthorizes a device for a <see cref="TraktUser"/>.
        /// </summary>
        /// <param name="traktUser">The authorizing <see cref="TraktUser"/>.</param>
        public async void DeauthorizeDevice(TraktUser traktUser)
        {
            var deviceRevokeRequest = new
            {
                token = traktUser.AccessToken,
                client_id = TraktUris.ClientId,
                client_secret = TraktUris.ClientSecret
            };

            await PostToTrakt<object>(TraktUris.RevokeToken, deviceRevokeRequest, traktUser).ConfigureAwait(false);
        }

        /// <summary>
        /// Poll access token status for a <see cref="TraktUser"/>.
        /// </summary>
        /// <param name="deviceCode">The <see cref="TraktDeviceCode"/>.</param>
        /// <param name="traktUser">The authorizing <see cref="TraktUser"/>.</param>
        /// <returns>Task{bool}.</returns>
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

        /// <summary>
        /// Refreshes the access token for a <see cref="TraktUser"/>.
        /// </summary>
        /// <param name="traktUser">The <see cref="TraktUser"/> to refresh the access token for.</param>
        /// <returns>Task.</returns>
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
                using var response = await PostToTrakt(TraktUris.AccessToken, data).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
#pragma warning disable CA2007
                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#pragma warning restore CA2007
                userAccessToken = await JsonSerializer.DeserializeAsync<TraktUserAccessToken>(stream, _jsonOptions).ConfigureAwait(false);
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

        private Task<T> GetFromTrakt<T>(string url, TraktUser traktUser)
        {
            return GetFromTrakt<T>(url, traktUser, CancellationToken.None);
        }

        private async Task<T> GetFromTrakt<T>(string url, TraktUser traktUser, CancellationToken cancellationToken)
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
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false);
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

        private Task<T> PostToTrakt<T>(string url, object data, TraktUser traktUser)
        {
            return PostToTrakt<T>(url, data, traktUser, CancellationToken.None);
        }

        /// <summary>
        /// Posts data to url, authenticating with <see cref="TraktUser"/>.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="data">The data object.</param>
        /// <param name="traktUser">The <see cref="TraktUser"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        private async Task<T> PostToTrakt<T>(
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
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handled in PostToTrakt");
                throw;
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
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
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
            retval.Tvdb = mediaObject.GetProviderId(MetadataProvider.Tvdb);
            retval.Tvrage = mediaObject.GetProviderId(MetadataProvider.TvRage);
            return retval;
        }

        private static TTraktShow FindShow<TTraktShow>(ICollection<TTraktShow> shows, Series series)
            where TTraktShow : TraktShow
        {
            return shows.FirstOrDefault(
                sre => sre.Ids != null
                && sre.Ids.Imdb == series.GetProviderId(MetadataProvider.Imdb)
                && sre.Ids.Tmdb == series.GetProviderId(MetadataProvider.Tmdb).ConvertToInt()
                && sre.Ids.Tvdb == series.GetProviderId(MetadataProvider.Tvdb)
                && sre.Ids.Tvrage == series.GetProviderId(MetadataProvider.TvRage));
        }

        private bool HasAnyProviderTvIds(BaseItem item)
        {
            return item.HasProviderId(MetadataProvider.Imdb)
                || item.HasProviderId(MetadataProvider.Tmdb)
                || item.HasProviderId(MetadataProvider.Tvdb)
                || item.HasProviderId(MetadataProvider.TvRage);
        }

        private bool HasAnyProviderTvIds(TraktTVId item)
        {
            return !string.IsNullOrEmpty(item.Imdb)
                || !(item.Tmdb == null)
                || !string.IsNullOrEmpty(item.Tvdb)
                || !string.IsNullOrEmpty(item.Tvrage);
        }
    }
}
