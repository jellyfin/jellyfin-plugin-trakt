using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Trakt.Api.DataContracts;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.Scrobble;
using Trakt.Api.DataContracts.Sync;
using Trakt.Api.DataContracts.Sync.Ratings;
using Trakt.Api.DataContracts.Sync.Watched;
using Trakt.Helpers;
using Trakt.Model;
using TraktEpisodeCollected = Trakt.Api.DataContracts.Sync.Collection.TraktEpisodeCollected;
using TraktMovieCollected = Trakt.Api.DataContracts.Sync.Collection.TraktMovieCollected;
using TraktShowCollected = Trakt.Api.DataContracts.Sync.Collection.TraktShowCollected;

namespace Trakt.Api
{
    /// <summary>
    ///
    /// </summary>
    public class TraktApi
    {
        private static readonly SemaphoreSlim _traktResourcePool = new SemaphoreSlim(1, 1);

        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger<TraktApi> _logger;
        private readonly IHttpClient _httpClient;
        private readonly IServerApplicationHost _appHost;
        private readonly IUserDataManager _userDataManager;
        private readonly IFileSystem _fileSystem;

        public TraktApi(
            IJsonSerializer jsonSerializer,
            ILogger<TraktApi> logger,
            IHttpClient httpClient,
            IServerApplicationHost appHost,
            IUserDataManager userDataManager,
            IFileSystem fileSystem)
        {
            _httpClient = httpClient;
            _appHost = appHost;
            _userDataManager = userDataManager;
            _fileSystem = fileSystem;
            _jsonSerializer = jsonSerializer;
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
                && (episode.IndexNumber.HasValue || !string.IsNullOrEmpty(episode.GetProviderId(MetadataProvider.Tvdb))))
            {
                var series = episode.Series;

                return !string.IsNullOrEmpty(series.GetProviderId(MetadataProvider.Imdb))
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
                app_date = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd"),
                app_version = _appHost.ApplicationVersionString,
                progress = progressPercent,
                movie = new TraktMovie
                {
                    title = movie.Name,
                    year = movie.ProductionYear,
                    ids = new TraktMovieId
                    {
                        imdb = movie.GetProviderId(MetadataProvider.Imdb),
                        tmdb = movie.GetProviderId(MetadataProvider.Tmdb).ConvertToInt()
                    }
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

            using (var response = await PostToTrakt(url, movieData, CancellationToken.None, traktUser).ConfigureAwait(false))
            {
                return _jsonSerializer.DeserializeFromStream<TraktScrobbleResponse>(response);
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
            var tvDbId = episode.GetProviderId(MetadataProvider.Tvdb);

            if (!string.IsNullOrEmpty(tvDbId) && (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue || episode.IndexNumberEnd <= episode.IndexNumber))
            {
                episodeDatas.Add(new TraktScrobbleEpisode
                {
                    app_date = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    app_version = _appHost.ApplicationVersionString,
                    progress = progressPercent,
                    episode = new TraktEpisode
                    {
                        ids = new TraktEpisodeId
                        {
                            tvdb = tvDbId.ConvertToInt()
                        },
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
                        app_date = DateTimeOffset.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        app_version = _appHost.ApplicationVersionString,
                        progress = progressPercent,
                        episode = new TraktEpisode
                        {
                            season = episode.GetSeasonNumber(),
                            number = number
                        },
                        show = new TraktShow
                        {
                            title = episode.Series.Name,
                            year = episode.Series.ProductionYear,
                            ids = new TraktShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProvider.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProvider.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProvider.TvRage).ConvertToInt()
                            }
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
                using (var response = await PostToTrakt(url, traktScrobbleEpisode, CancellationToken.None, traktUser).ConfigureAwait(false))
                {
                    responses.Add(_jsonSerializer.DeserializeFromStream<TraktScrobbleResponse>(response));
                }
            }
            return responses;
        }

        /// <summary>
        /// Add or remove a list of movies to/from the users trakt.tv library
        /// </summary>
        /// <param name="movies">The movies to add</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{TraktResponseDataContract}.</returns>
        public async Task<IEnumerable<TraktSyncResponse>> SendLibraryUpdateAsync(
            IList<Movie> movies,
            TraktUser traktUser,
            CancellationToken cancellationToken,
            EventType eventType)
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
                    collected_at = m.DateCreated.ToISO8601(),
                    title = m.Name,
                    year = m.ProductionYear,
                    ids = new TraktMovieId
                    {
                        imdb = m.GetProviderId(MetadataProvider.Imdb),
                        tmdb = m.GetProviderId(MetadataProvider.Tmdb).ConvertToInt()
                    }
                };
                if (traktUser.ExportMediaInfo)
                {
                    traktMovieCollected.audio_channels = audioStream.GetAudioChannels();
                    traktMovieCollected.audio = audioStream.GetCodecRepresetation();
                    traktMovieCollected.resolution = m.GetDefaultVideoStream().GetResolution();
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
                    movies = chunk.ToList()
                };
                using (var response = await PostToTrakt(url, data, cancellationToken, traktUser).ConfigureAwait(false))
                {
                    responses.Add(_jsonSerializer.DeserializeFromStream<TraktSyncResponse>(response));
                }
            }

            return responses;
        }

        /// <summary>
        /// Add or remove a list of Episodes to/from the users trakt.tv library
        /// </summary>
        /// <param name="episodes">The episodes to add</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{TraktResponseDataContract}.</returns>
        public async Task<IEnumerable<TraktSyncResponse>> SendLibraryUpdateAsync(
            IReadOnlyList<Episode> episodes,
            TraktUser traktUser,
            CancellationToken cancellationToken,
            EventType eventType)
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
                responses.Add(await SendLibraryUpdateInternalAsync(chunk.ToList(), traktUser, cancellationToken, eventType).ConfigureAwait(false));
            }
            return responses;
        }

        private async Task<TraktSyncResponse> SendLibraryUpdateInternalAsync(
            IEnumerable<Episode> episodes,
            TraktUser traktUser,
            CancellationToken cancellationToken,
            EventType eventType)
        {
            var episodesPayload = new List<TraktEpisodeCollected>();
            var showPayload = new List<TraktShowCollected>();
            foreach (Episode episode in episodes)
            {
                var audioStream = episode.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);
                var tvDbId = episode.GetProviderId(MetadataProvider.Tvdb);

                if (!string.IsNullOrEmpty(tvDbId) &&
                    (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue ||
                     episode.IndexNumberEnd <= episode.IndexNumber))
                {
                    var traktEpisodeCollected = new TraktEpisodeCollected
                    {
                        collected_at = episode.DateCreated.ToISO8601(),
                        ids = new TraktEpisodeId
                        {
                            tvdb = tvDbId.ConvertToInt()
                        }
                    };
                    if (traktUser.ExportMediaInfo)
                    {
                        //traktEpisodeCollected.Is3D = episode.Is3D;
                        traktEpisodeCollected.audio_channels = audioStream.GetAudioChannels();
                        traktEpisodeCollected.audio = audioStream.GetCodecRepresetation();
                        traktEpisodeCollected.resolution = episode.GetDefaultVideoStream().GetResolution();
                    }

                    episodesPayload.Add(traktEpisodeCollected);
                }
                else if (episode.IndexNumber.HasValue)
                {
                    var indexNumber = episode.IndexNumber.Value;
                    var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;
                    var syncShow =
                        showPayload.FirstOrDefault(
                            sre =>
                                sre.ids != null &&
                                sre.ids.tvdb == episode.Series.GetProviderId(MetadataProvider.Tvdb).ConvertToInt());
                    if (syncShow == null)
                    {
                        syncShow = new TraktShowCollected
                        {
                            ids = new TraktShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProvider.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProvider.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProvider.TvRage).ConvertToInt()
                            },
                            seasons = new List<TraktShowCollected.TraktSeasonCollected>()
                        };

                        showPayload.Add(syncShow);
                    }
                    var syncSeason =
                        syncShow.seasons.FirstOrDefault(ss => ss.number == episode.GetSeasonNumber());
                    if (syncSeason == null)
                    {
                        syncSeason = new TraktShowCollected.TraktSeasonCollected
                        {
                            number = episode.GetSeasonNumber(),
                            episodes = new List<TraktEpisodeCollected>()
                        };

                        syncShow.seasons.Add(syncSeason);
                    }
                    for (var number = indexNumber; number <= finalNumber; number++)
                    {
                        var ids = new TraktEpisodeId();

                        if (number == indexNumber)
                        {
                            // Omit this from the rest because then we end up attaching the tvdb of the first episode to the subsequent ones
                            ids.tvdb = tvDbId.ConvertToInt();
                        }

                        var traktEpisodeCollected = new TraktEpisodeCollected
                        {
                            number = number,
                            collected_at = episode.DateCreated.ToISO8601(),
                            ids = ids
                        };
                        if (traktUser.ExportMediaInfo)
                        {
                            //traktEpisodeCollected.Is3D = episode.Is3D;
                            traktEpisodeCollected.audio_channels = audioStream.GetAudioChannels();
                            traktEpisodeCollected.audio = audioStream.GetCodecRepresetation();
                            traktEpisodeCollected.resolution = episode.GetDefaultVideoStream().GetResolution();
                        }

                        syncSeason.episodes.Add(traktEpisodeCollected);
                    }
                }
            }

            var data = new TraktSyncCollected
            {
                episodes = episodesPayload.ToList(),
                shows = showPayload.ToList()
            };

            var url = eventType == EventType.Add ? TraktUris.SyncCollectionAdd : TraktUris.SyncCollectionRemove;
            using (var response = await PostToTrakt(url, data, cancellationToken, traktUser).ConfigureAwait(false))
            {
                return _jsonSerializer.DeserializeFromStream<TraktSyncResponse>(response);
            }
        }

        /// <summary>
        /// Add or remove a Show(Series) to/from the users trakt.tv library
        /// </summary>
        /// <param name="show">The show to remove</param>
        /// <param name="traktUser">The user who's library is being updated</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventType"></param>
        /// <returns>Task{TraktResponseDataContract}.</returns>
        public async Task<TraktSyncResponse> SendLibraryUpdateAsync(
            Series show,
            TraktUser traktUser,
            CancellationToken cancellationToken,
            EventType eventType)
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
                    title = show.Name,
                    year = show.ProductionYear,
                    ids = new TraktShowId
                    {
                        tvdb = show.GetProviderId(MetadataProvider.Tvdb).ConvertToInt(),
                        imdb = show.GetProviderId(MetadataProvider.Imdb),
                        tvrage = show.GetProviderId(MetadataProvider.TvRage).ConvertToInt()
                    },
                }
            };

            var data = new TraktSyncCollected
            {
                shows = showPayload.ToList()
            };

            var url = eventType == EventType.Add ? TraktUris.SyncCollectionAdd : TraktUris.SyncCollectionRemove;
            using (var response = await PostToTrakt(url, data, cancellationToken, traktUser).ConfigureAwait(false))
            {
                return _jsonSerializer.DeserializeFromStream<TraktSyncResponse>(response);
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
                            title = item.Name,
                            year = item.ProductionYear,
                            ids = new TraktMovieId
                            {
                                imdb = item.GetProviderId(MetadataProvider.Imdb),
                                tmdb = item.GetProviderId(MetadataProvider.Tmdb).ConvertToInt()
                            },
                            rating = rating
                        }
                    }
                };

            }
            else if (item is Episode)
            {
                var episode = item as Episode;

                if (string.IsNullOrEmpty(episode.GetProviderId(MetadataProvider.Tvdb)))
                {
                    if (episode.IndexNumber.HasValue)
                    {
                        var indexNumber = episode.IndexNumber.Value;
                        var show = new TraktShowRated
                        {
                            ids = new TraktShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProvider.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProvider.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProvider.TvRage).ConvertToInt()
                            },
                            seasons = new List<TraktShowRated.TraktSeasonRated>
                            {
                                new TraktShowRated.TraktSeasonRated
                                {
                                    number = episode.GetSeasonNumber(),
                                    episodes = new List<TraktEpisodeRated>
                                    {
                                        new TraktEpisodeRated
                                        {
                                            number = indexNumber,
                                            rating = rating
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
                else
                {
                    data = new
                    {
                        episodes = new[]
                        {
                            new TraktEpisodeRated
                            {
                                rating = rating,
                                ids = new TraktEpisodeId
                                {
                                    tvdb = episode.GetProviderId(MetadataProvider.Tvdb).ConvertToInt()
                                }
                            }
                        }
                    };
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
                            rating = rating,
                            title = item.Name,
                            year = item.ProductionYear,
                            ids = new TraktShowId
                            {
                                imdb = item.GetProviderId(MetadataProvider.Imdb),
                                tvdb = item.GetProviderId(MetadataProvider.Tvdb).ConvertToInt()
                            }
                        }
                    }
                };
            }

            using (var response = await PostToTrakt(TraktUris.SyncRatingsAdd, data, traktUser).ConfigureAwait(false))
            {
                return _jsonSerializer.DeserializeFromStream<TraktSyncResponse>(response);
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
                return _jsonSerializer.DeserializeFromStream<List<TraktMovie>>(response);
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
                return _jsonSerializer.DeserializeFromStream<List<TraktShow>>(response);
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
                return _jsonSerializer.DeserializeFromStream<List<DataContracts.Users.Watched.TraktMovieWatched>>(response);
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
                return _jsonSerializer.DeserializeFromStream<List<DataContracts.Users.Watched.TraktShowWatched>>(response);
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
                return _jsonSerializer.DeserializeFromStream<List<DataContracts.Users.Collection.TraktMovieCollected>>(response);
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
                return _jsonSerializer.DeserializeFromStream<List<DataContracts.Users.Collection.TraktShowCollected>>(response);
            }
        }

        private int? ParseId(string value)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
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
                    title = m.Name,
                    ids = new TraktMovieId
                    {
                        imdb = m.GetProviderId(MetadataProvider.Imdb),
                        tmdb =
                            string.IsNullOrEmpty(m.GetProviderId(MetadataProvider.Tmdb))
                                ? (int?)null
                                : ParseId(m.GetProviderId(MetadataProvider.Tmdb))
                    },
                    year = m.ProductionYear,
                    watched_at = lastPlayedDate?.ToISO8601()
                };
            }).ToList();
            var chunks = moviesPayload.ToChunks(100).ToList();
            var traktResponses = new List<TraktSyncResponse>();

            foreach (var chunk in chunks)
            {
                var data = new TraktSyncWatched
                {
                    movies = chunk.ToList()
                };
                var url = seen ? TraktUris.SyncWatchedHistoryAdd : TraktUris.SyncWatchedHistoryRemove;

                using (var response = await PostToTrakt(url, data, cancellationToken, traktUser).ConfigureAwait(false))
                {
                    if (response != null)
                    {
                        traktResponses.Add(_jsonSerializer.DeserializeFromStream<TraktSyncResponse>(response));
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
            var data = new TraktSyncWatched { episodes = new List<TraktEpisodeWatched>(), shows = new List<TraktShowWatched>() };
            foreach (var episode in episodeChunk)
            {
                var tvDbId = episode.GetProviderId(MetadataProvider.Tvdb);
                var lastPlayedDate = seen
                    ? _userDataManager.GetUserData(new Guid(traktUser.LinkedMbUserId), episode)
                        .LastPlayedDate
                    : null;
                if (!string.IsNullOrEmpty(tvDbId) && (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue || episode.IndexNumberEnd <= episode.IndexNumber))
                {
                    data.episodes.Add(new TraktEpisodeWatched
                    {
                        ids = new TraktEpisodeId
                        {
                            tvdb = int.Parse(tvDbId)
                        },
                        watched_at = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : null
                    });
                }
                else if (episode.IndexNumber != null)
                {
                    var indexNumber = episode.IndexNumber.Value;
                    var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;

                    var syncShow = data.shows.FirstOrDefault(sre => sre.ids != null && sre.ids.tvdb == episode.Series.GetProviderId(MetadataProvider.Tvdb).ConvertToInt());
                    if (syncShow == null)
                    {
                        syncShow = new TraktShowWatched
                        {
                            ids = new TraktShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProvider.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProvider.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProvider.TvRage).ConvertToInt()
                            },
                            seasons = new List<TraktSeasonWatched>()
                        };
                        data.shows.Add(syncShow);
                    }

                    var syncSeason = syncShow.seasons.FirstOrDefault(ss => ss.number == episode.GetSeasonNumber());
                    if (syncSeason == null)
                    {
                        syncSeason = new TraktSeasonWatched
                        {
                            number = episode.GetSeasonNumber(),
                            episodes = new List<TraktEpisodeWatched>()
                        };
                        syncShow.seasons.Add(syncSeason);
                    }

                    for (var number = indexNumber; number <= finalNumber; number++)
                    {
                        syncSeason.episodes.Add(new TraktEpisodeWatched
                        {
                            number = number,
                            watched_at = lastPlayedDate.HasValue ? lastPlayedDate.Value.ToISO8601() : null
                        });
                    }
                }
            }
            var url = seen ? TraktUris.SyncWatchedHistoryAdd : TraktUris.SyncWatchedHistoryRemove;

            using (var response = await PostToTrakt(url, data, cancellationToken, traktUser).ConfigureAwait(false))
            {
                return _jsonSerializer.DeserializeFromStream<TraktSyncResponse>(response);
            }
        }

        public string AuthorizeDevice(TraktUser traktUser)
        {
            var deviceCodeRequest = new
            {
                client_id = TraktUris.ClientId
            };

            TraktDeviceCode deviceCode;
            using (var response = PostToTrakt(TraktUris.DeviceCode, deviceCodeRequest, null))
            {
                deviceCode = _jsonSerializer.DeserializeFromStream<TraktDeviceCode>(response.Result);
            }

            // Start polling in the background
            Plugin.Instance.PollingTasks[traktUser.LinkedMbUserId] = Task.Run(() => PollForAccessToken(deviceCode, traktUser));

            return deviceCode.user_code;
        }

        public async Task<bool> PollForAccessToken(TraktDeviceCode deviceCode, TraktUser traktUser)
        {
            var deviceAccessTokenRequest = new
            {
                code = deviceCode.device_code,
                client_id = TraktUris.ClientId,
                client_secret = TraktUris.ClientSecret
            };

            var pollingInterval = deviceCode.interval;
            var expiresAt = DateTime.UtcNow.AddSeconds(deviceCode.expires_in);
            _logger.LogInformation("Polling for access token every {PollingInterval}s. Expires at {ExpiresAt} UTC.", pollingInterval, expiresAt);
            while (DateTime.UtcNow < expiresAt)
            {
                try
                {
                    using (var response = await PostToTrakt(TraktUris.DeviceToken, deviceAccessTokenRequest).ConfigureAwait(false))
                    {
                        _logger.LogInformation("Device successfully authorized");

                        var userAccessToken = _jsonSerializer.DeserializeFromStream<TraktUserAccessToken>(response.Content);
                        if (userAccessToken != null)
                        {
                            traktUser.AccessToken = userAccessToken.access_token;
                            traktUser.RefreshToken = userAccessToken.refresh_token;
                            traktUser.AccessTokenExpiration = DateTime.Now.AddSeconds(userAccessToken.expirationWithBuffer);
                            Plugin.Instance.SaveConfiguration();
                            return true;
                        }
                    }
                }
                catch (HttpException e)
                {
                    switch (e.StatusCode)
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
                        default:
                            _logger.LogError(e, "Unexpected error when authorizing device");
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
                client_id = TraktUris.ClientId,
                client_secret = TraktUris.ClientSecret,
                redirect_uri = "urn:ietf:wg:oauth:2.0:oob",
                refresh_token = traktUser.RefreshToken,
                grant_type = "refresh_token"
            };

            TraktUserAccessToken userAccessToken;
            try
            {
                using (var response = await PostToTrakt(TraktUris.AccessToken, data).ConfigureAwait(false))
                {
                    userAccessToken = _jsonSerializer.DeserializeFromStream<TraktUserAccessToken>(response.Content);
                }

            }
            catch (HttpException ex)
            {
                _logger.LogError(ex, "An error occurred during token refresh");
                return;
            }

            if (userAccessToken != null)
            {
                traktUser.AccessToken = userAccessToken.access_token;
                traktUser.RefreshToken = userAccessToken.refresh_token;
                traktUser.AccessTokenExpiration = DateTime.Now.AddSeconds(userAccessToken.expirationWithBuffer);
                Plugin.Instance.SaveConfiguration();
                _logger.LogInformation("Successfully refreshed the access token for user {UserId}", traktUser.LinkedMbUserId);
            }
        }

        private Task<Stream> GetFromTrakt(string url, TraktUser traktUser)
        {
            return GetFromTrakt(url, CancellationToken.None, traktUser);
        }

        private async Task<Stream> GetFromTrakt(string url, CancellationToken cancellationToken, TraktUser traktUser)
        {
            var options = GetHttpRequestOptions();
            options.Url = url;
            options.CancellationToken = cancellationToken;

            if (traktUser != null)
            {
                await SetRequestHeaders(options, traktUser).ConfigureAwait(false);
            }

            await _traktResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                return await Retry(async () => await _httpClient.Get(options).ConfigureAwait(false)).ConfigureAwait(false);
            }
            finally
            {
                _traktResourcePool.Release();
            }
        }

        private async Task<HttpResponseInfo> PostToTrakt(string url, object data)
        {
            var requestContent = data == null ? string.Empty : _jsonSerializer.SerializeToString(data);
            var options = GetHttpRequestOptions();
            options.Url = url;
            options.CancellationToken = CancellationToken.None;
            options.RequestContent = requestContent;

            await _traktResourcePool.WaitAsync(options.CancellationToken).ConfigureAwait(false);

            try
            {
                return await _httpClient.Post(options).ConfigureAwait(false);
            }
            finally
            {
                _traktResourcePool.Release();
            }
        }

        private Task<Stream> PostToTrakt(string url, object data, TraktUser traktUser)
        {
            return PostToTrakt(url, data, CancellationToken.None, traktUser);
        }

        /// <summary>
        ///     Posts data to url, authenticating with <see cref="TraktUser"/>.
        /// </summary>
        /// <param name="traktUser">If null, authentication headers not added.</param>
        private async Task<Stream> PostToTrakt(
            string url,
            object data,
            CancellationToken cancellationToken,
            TraktUser traktUser)
        {
            var requestContent = data == null ? string.Empty : _jsonSerializer.SerializeToString(data);
            if (traktUser != null && traktUser.ExtraLogging)
            {
                _logger.LogDebug(requestContent);
            }

            var options = GetHttpRequestOptions();
            options.Url = url;
            options.CancellationToken = cancellationToken;
            options.RequestContent = requestContent;

            if (traktUser != null)
            {
                await SetRequestHeaders(options, traktUser).ConfigureAwait(false);
            }

            await _traktResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var retryResponse = await Retry(async () => await _httpClient.Post(options).ConfigureAwait(false)).ConfigureAwait(false);
                return retryResponse.Content;
            }
            finally
            {
                _traktResourcePool.Release();
            }
        }

        private async Task<T> Retry<T>(Func<Task<T>> function)
        {
            try
            {
                return await function().ConfigureAwait(false);
            }
            catch
            {
            }

            await Task.Delay(500).ConfigureAwait(false);
            try
            {
                return await function().ConfigureAwait(false);
            }
            catch
            {
            }

            await Task.Delay(500).ConfigureAwait(false);
            return await function().ConfigureAwait(false);
        }

        private HttpRequestOptions GetHttpRequestOptions()
        {
            var options = new HttpRequestOptions
            {
                RequestContentType = "application/json",
                LogErrorResponseBody = false,
                BufferContent = false,
                DecompressionMethod = CompressionMethods.None,
                EnableKeepAlive = false
            };

            options.RequestHeaders.Add("trakt-api-version", "2");
            options.RequestHeaders.Add("trakt-api-key", TraktUris.ClientId);
            return options;
        }

        private async Task SetRequestHeaders(HttpRequestOptions options, TraktUser traktUser)
        {
            if (DateTimeOffset.Now > traktUser.AccessTokenExpiration)
            {
                traktUser.AccessToken = string.Empty;
                await RefreshUserAccessToken(traktUser).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(traktUser.AccessToken))
            {
                options.RequestHeaders.Add("Authorization", "Bearer " + traktUser.AccessToken);
            }
        }
    }
}
