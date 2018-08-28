using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Trakt.Api.DataContracts;
using Trakt.Api.DataContracts.BaseModel;
using Trakt.Api.DataContracts.Scrobble;
using Trakt.Api.DataContracts.Sync;
using Trakt.Api.DataContracts.Sync.Ratings;
using Trakt.Api.DataContracts.Sync.Watched;
using Trakt.Helpers;
using Trakt.Model;
using MediaBrowser.Model.Entities;
using TraktMovieCollected = Trakt.Api.DataContracts.Sync.Collection.TraktMovieCollected;
using TraktEpisodeCollected = Trakt.Api.DataContracts.Sync.Collection.TraktEpisodeCollected;
using TraktShowCollected = Trakt.Api.DataContracts.Sync.Collection.TraktShowCollected;
using MediaBrowser.Model.IO;

namespace Trakt.Api
{
    /// <summary>
    /// 
    /// </summary>
    public class TraktApi
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private readonly IServerApplicationHost _appHost;
        private readonly IUserDataManager _userDataManager;
        private readonly IFileSystem _fileSystem;

        public TraktApi(IJsonSerializer jsonSerializer, ILogger logger, IHttpClient httpClient,
            IServerApplicationHost appHost, IUserDataManager userDataManager, IFileSystem fileSystem)
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

            var movie = item as Movie;

            if (movie != null)
            {
                return !string.IsNullOrEmpty(movie.GetProviderId(MetadataProviders.Imdb)) ||
                    !string.IsNullOrEmpty(movie.GetProviderId(MetadataProviders.Tmdb));
            }

            var episode = item as Episode;

            if (episode != null && episode.Series != null && !episode.IsMissingEpisode && (episode.IndexNumber.HasValue || !string.IsNullOrEmpty(episode.GetProviderId(MetadataProviders.Tvdb))))
            {
                var series = episode.Series;

                return !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Imdb)) ||
                    !string.IsNullOrEmpty(series.GetProviderId(MetadataProviders.Tvdb));
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
                app_date = DateTime.Today.ToString("yyyy-MM-dd"),
                app_version = _appHost.ApplicationVersion.ToString(),
                progress = progressPercent,
                movie = new TraktMovie
                {
                    title = movie.Name,
                    year = movie.ProductionYear,
                    ids = new TraktMovieId
                    {
                        imdb = movie.GetProviderId(MetadataProviders.Imdb),
                        tmdb = movie.GetProviderId(MetadataProviders.Tmdb).ConvertToInt()
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
            var tvDbId = episode.GetProviderId(MetadataProviders.Tvdb);

            if (!string.IsNullOrEmpty(tvDbId) && (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue || episode.IndexNumberEnd <= episode.IndexNumber))
            {
                episodeDatas.Add(new TraktScrobbleEpisode
                {
                    app_date = DateTime.Today.ToString("yyyy-MM-dd"),
                    app_version = _appHost.ApplicationVersion.ToString(),
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
                        app_date = DateTime.Today.ToString("yyyy-MM-dd"),
                        app_version = _appHost.ApplicationVersion.ToString(),
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
                                tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
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
        public async Task<IEnumerable<TraktSyncResponse>> SendLibraryUpdateAsync(List<Movie> movies, TraktUser traktUser,
            CancellationToken cancellationToken, EventType eventType)
        {
            if (movies == null)
                throw new ArgumentNullException("movies");
            if (traktUser == null)
                throw new ArgumentNullException("traktUser");

            if (eventType == EventType.Update) return null;

            var moviesPayload = movies.Select(m =>
            {
                var audioStream = m.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);
                var traktMovieCollected = new TraktMovieCollected
                {
                    collected_at = m.DateCreated.UtcDateTime.ToISO8601(),
                    title = m.Name,
                    year = m.ProductionYear,
                    ids = new TraktMovieId
                    {
                        imdb = m.GetProviderId(MetadataProviders.Imdb),
                        tmdb = m.GetProviderId(MetadataProviders.Tmdb).ConvertToInt()
                    }
                };
                if (traktUser.ExportMediaInfo)
                {
                    //traktMovieCollected.Is3D = m.Is3D;
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
        public async Task<IEnumerable<TraktSyncResponse>> SendLibraryUpdateAsync(IReadOnlyList<Episode> episodes,
            TraktUser traktUser, CancellationToken cancellationToken, EventType eventType)
        {
            if (episodes == null)
                throw new ArgumentNullException("episodes");

            if (traktUser == null)
                throw new ArgumentNullException("traktUser");

            if (eventType == EventType.Update) return null;
            var responses = new List<TraktSyncResponse>();
            var chunks = episodes.ToChunks(100);
            foreach (var chunk in chunks)
            {
                responses.Add(await SendLibraryUpdateInternalAsync(chunk.ToList(), traktUser, cancellationToken, eventType).ConfigureAwait(false));
            }
            return responses;
        }

        private async Task<TraktSyncResponse> SendLibraryUpdateInternalAsync(IEnumerable<Episode> episodes,
            TraktUser traktUser, CancellationToken cancellationToken, EventType eventType)
        {
            var episodesPayload = new List<TraktEpisodeCollected>();
            var showPayload = new List<TraktShowCollected>();
            foreach (Episode episode in episodes)
            {
                var audioStream = episode.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);
                var tvDbId = episode.GetProviderId(MetadataProviders.Tvdb);

                if (!string.IsNullOrEmpty(tvDbId) &&
                    (!episode.IndexNumber.HasValue || !episode.IndexNumberEnd.HasValue ||
                     episode.IndexNumberEnd <= episode.IndexNumber))
                {
                    var traktEpisodeCollected = new TraktEpisodeCollected
                    {
                        collected_at = episode.DateCreated.UtcDateTime.ToISO8601(),
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
                                sre.ids.tvdb == episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt());
                    if (syncShow == null)
                    {
                        syncShow = new TraktShowCollected
                        {
                            ids = new TraktShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
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
                            collected_at = episode.DateCreated.UtcDateTime.ToISO8601(),
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
        public async Task<TraktSyncResponse> SendLibraryUpdateAsync(Series show, TraktUser traktUser, CancellationToken cancellationToken, EventType eventType)
        {
            if (show == null)
                throw new ArgumentNullException("show");
            if (traktUser == null)
                throw new ArgumentNullException("traktUser");

            if (eventType == EventType.Update) return null;

            var showPayload = new List<TraktShowCollected>
            {
                new TraktShowCollected
                {
                    title = show.Name,
                    year = show.ProductionYear,
                    ids = new TraktShowId
                    {
                        tvdb = show.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                        imdb = show.GetProviderId(MetadataProviders.Imdb),
                        tvrage = show.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
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
                                imdb = item.GetProviderId(MetadataProviders.Imdb),
                                tmdb = item.GetProviderId(MetadataProviders.Tmdb).ConvertToInt()
                            },
                            rating = rating
                        }
                    }
                };

            }
            else if (item is Episode)
            {
                var episode = item as Episode;

                if (string.IsNullOrEmpty(episode.GetProviderId(MetadataProviders.Tvdb)))
                {
                    if (episode.IndexNumber.HasValue)
                    {
                        var indexNumber = episode.IndexNumber.Value;
                        var show = new TraktShowRated
                        {
                            ids = new TraktShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
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
                                    tvdb = episode.GetProviderId(MetadataProviders.Tvdb).ConvertToInt()
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
                                imdb = item.GetProviderId(MetadataProviders.Imdb),
                                tvdb = item.GetProviderId(MetadataProviders.Tvdb).ConvertToInt()
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
        /// <param name="item"></param>
        /// <param name="comment"></param>
        /// <param name="containsSpoilers"></param>
        /// <param name="traktUser"></param>
        /// <param name="isReview"></param>
        /// <returns></returns>
        public async Task<object> SendItemComment(BaseItem item, string comment, bool containsSpoilers, TraktUser traktUser, bool isReview = false)
        {
            return null;
            //TODO: This functionallity is not available yet
            //            string url;
            //            var data = new Dictionary<string, string>
            //                           {
            //                               {"username", traktUser.UserName},
            //                               {"password", traktUser.Password}
            //                           };
            //
            //            if (item is Movie)
            //            {
            //                if (item.ProviderIds != null && item.ProviderIds.ContainsKey("Imdb"))
            //                    data.Add("imdb_id", item.ProviderIds["Imdb"]);
            //                
            //                data.Add("title", item.Name);
            //                data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
            //                url = TraktUris.CommentMovie;
            //            }
            //            else
            //            {
            //                var episode = item as Episode;
            //                if (episode != null)
            //                {
            //                    if (episode.Series.ProviderIds != null)
            //                    {
            //                        if (episode.Series.ProviderIds.ContainsKey("Imdb"))
            //                            data.Add("imdb_id", episode.Series.ProviderIds["Imdb"]);
            //
            //                        if (episode.Series.ProviderIds.ContainsKey("Tvdb"))
            //                            data.Add("tvdb_id", episode.Series.ProviderIds["Tvdb"]);
            //                    }
            //
            //                    data.Add("season", episode.AiredSeasonNumber.ToString());
            //                    data.Add("episode", episode.IndexNumber.ToString());
            //                    url = TraktUris.CommentEpisode;   
            //                }
            //                else // It's a Series
            //                {
            //                    data.Add("title", item.Name);
            //                    data.Add("year", item.ProductionYear != null ? item.ProductionYear.ToString() : "");
            //
            //                    if (item.ProviderIds != null)
            //                    {
            //                        if (item.ProviderIds.ContainsKey("Imdb"))
            //                            data.Add("imdb_id", item.ProviderIds["Imdb"]);
            //
            //                        if (item.ProviderIds.ContainsKey("Tvdb"))
            //                            data.Add("tvdb_id", item.ProviderIds["Tvdb"]);
            //                    }
            //                    
            //                    url = TraktUris.CommentShow;
            //                }
            //            }
            //
            //            data.Add("comment", comment);
            //            data.Add("spoiler", containsSpoilers.ToString());
            //            data.Add("review", isReview.ToString());
            //
            //            Stream response =
            //                await
            //                _httpClient.Post(url, data, Plugin.Instance.TraktResourcePool,
            //                                                 CancellationToken.None).ConfigureAwait(false);
            //
            //            return _jsonSerializer.DeserializeFromStream<TraktResponseDataContract>(response);
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
            int parsed;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// Send a list of movies to trakt.tv that have been marked watched or unwatched
        /// </summary>
        /// <param name="movies">The list of movies to send</param>
        /// <param name="traktUser">The trakt user profile that is being updated</param>
        /// <param name="seen">True if movies are being marked seen, false otherwise</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns></returns>
        public async Task<List<TraktSyncResponse>> SendMoviePlaystateUpdates(List<Movie> movies, TraktUser traktUser, bool forceUpdate, bool seen, CancellationToken cancellationToken)
        {
            if (movies == null)
                throw new ArgumentNullException("movies");
            if (traktUser == null)
                throw new ArgumentNullException("traktUser");
            if (!forceUpdate && !traktUser.PostWatchedHistory)
                return new List<TraktSyncResponse>();

            var moviesPayload = movies.Select(m =>
            {
                var lastPlayedDate = seen
                    ? _userDataManager.GetUserData(traktUser.LinkedMbUserId, m).LastPlayedDate
                    : null;
                return new TraktMovieWatched
                {
                    title = m.Name,
                    ids = new TraktMovieId
                    {
                        imdb = m.GetProviderId(MetadataProviders.Imdb),
                        tmdb =
                            string.IsNullOrEmpty(m.GetProviderId(MetadataProviders.Tmdb))
                                ? (int?)null
                                : ParseId(m.GetProviderId(MetadataProviders.Tmdb))
                    },
                    year = m.ProductionYear,
                    watched_at = lastPlayedDate.HasValue ? lastPlayedDate.Value.UtcDateTime.ToISO8601() : null
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
                        traktResponses.Add(_jsonSerializer.DeserializeFromStream<TraktSyncResponse>(response));
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
        public async Task<List<TraktSyncResponse>> SendEpisodePlaystateUpdates(List<Episode> episodes, TraktUser traktUser, bool forceUpdate, bool seen, CancellationToken cancellationToken)
        {
            if (episodes == null)
                throw new ArgumentNullException("episodes");

            if (traktUser == null)
                throw new ArgumentNullException("traktUser");
            if (!forceUpdate && !traktUser.PostWatchedHistory)
                return new List<TraktSyncResponse>();

            var chunks = episodes.ToChunks(100).ToList();
            var traktResponses = new List<TraktSyncResponse>();

            foreach (var chunk in chunks)
            {
                var response = await SendEpisodePlaystateUpdatesInternalAsync(chunk, traktUser, seen, cancellationToken).ConfigureAwait(false);

                if (response != null)
                    traktResponses.Add(response);
            }
            return traktResponses;
        }


        private async Task<TraktSyncResponse> SendEpisodePlaystateUpdatesInternalAsync(IEnumerable<Episode> episodeChunk, TraktUser traktUser, bool seen, CancellationToken cancellationToken)
        {
            var data = new TraktSyncWatched { episodes = new List<TraktEpisodeWatched>(), shows = new List<TraktShowWatched>() };
            foreach (var episode in episodeChunk)
            {
                var tvDbId = episode.GetProviderId(MetadataProviders.Tvdb);
                var lastPlayedDate = seen
                    ? _userDataManager.GetUserData(traktUser.LinkedMbUserId, episode)
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
                        watched_at = lastPlayedDate.HasValue ? lastPlayedDate.Value.UtcDateTime.ToISO8601() : null
                    });
                }
                else if (episode.IndexNumber != null)
                {
                    var indexNumber = episode.IndexNumber.Value;
                    var finalNumber = (episode.IndexNumberEnd ?? episode.IndexNumber).Value;

                    var syncShow = data.shows.FirstOrDefault(sre => sre.ids != null && sre.ids.tvdb == episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt());
                    if (syncShow == null)
                    {
                        syncShow = new TraktShowWatched
                        {
                            ids = new TraktShowId
                            {
                                tvdb = episode.Series.GetProviderId(MetadataProviders.Tvdb).ConvertToInt(),
                                imdb = episode.Series.GetProviderId(MetadataProviders.Imdb),
                                tvrage = episode.Series.GetProviderId(MetadataProviders.TvRage).ConvertToInt()
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
                            watched_at = lastPlayedDate.HasValue ? lastPlayedDate.Value.UtcDateTime.ToISO8601() : null
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

        public async Task RefreshUserAuth(TraktUser traktUser)
        {
            var data = new TraktUserTokenRequest
            {
                client_id = TraktUris.Id,
                client_secret = TraktUris.Secret,
                redirect_uri = "urn:ietf:wg:oauth:2.0:oob"
            };

            if (!string.IsNullOrWhiteSpace(traktUser.PIN))
            {
                data.code = traktUser.PIN;
                data.grant_type = "authorization_code";
            }
            else if (!string.IsNullOrWhiteSpace(traktUser.RefreshToken))
            {
                data.refresh_token = traktUser.RefreshToken;
                data.grant_type = "refresh_token";
            }
            else
            {
                _logger.Error("Tried to reauthenticate with Trakt, but neither PIN nor refreshToken was available");
            }

            TraktUserToken userToken;
            using (var response = await PostToTrakt(TraktUris.Token, data, null).ConfigureAwait(false))
            {
                userToken = _jsonSerializer.DeserializeFromStream<TraktUserToken>(response);
            }

            if (userToken != null)
            {
                traktUser.AccessToken = userToken.access_token;
                traktUser.RefreshToken = userToken.refresh_token;
                traktUser.PIN = null;
                traktUser.AccessTokenExpiration = DateTime.Now.AddMonths(2);
                Plugin.Instance.SaveConfiguration();
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

            return await Retry(async () => await _httpClient.Get(options).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private Task<Stream> PostToTrakt(string url, object data, TraktUser traktUser)
        {
            return PostToTrakt(url, data, CancellationToken.None, traktUser);
        }

        /// <summary>
        ///     Posts data to url, authenticating with <see cref="TraktUser"/>.
        /// </summary>
        /// <param name="traktUser">If null, authentication headers not added.</param>
        private async Task<Stream> PostToTrakt(string url, object data, CancellationToken cancellationToken,
            TraktUser traktUser)
        {
            var requestContent = data == null ? string.Empty : _jsonSerializer.SerializeToString(data);
            if (traktUser != null && traktUser.ExtraLogging) _logger.Debug(requestContent);
            var options = GetHttpRequestOptions();
            options.Url = url;
            options.CancellationToken = cancellationToken;
            options.RequestContent = requestContent.AsMemory();

            if (traktUser != null)
            {
                await SetRequestHeaders(options, traktUser).ConfigureAwait(false);
            }
            
            var retryResponse = await Retry(async ()=> await _httpClient.Post(options).ConfigureAwait(false)).ConfigureAwait(false);
            return retryResponse.Content;
        }

        private async Task<T> Retry<T>(Func<Task<T>> function)
        {
            try
            {
                return await function().ConfigureAwait(false);
            }
            catch{}
            await Task.Delay(500).ConfigureAwait(false);
            try
            {
                return await function().ConfigureAwait(false);
            }
            catch { }
            await Task.Delay(500).ConfigureAwait(false);
            return await function().ConfigureAwait(false);
        }

        private HttpRequestOptions GetHttpRequestOptions()
        {
            var options = new HttpRequestOptions
            {
                ResourcePool = Plugin.Instance.TraktResourcePool,
                RequestContentType = "application/json",
                TimeoutMs = 120000,
                LogErrorResponseBody = false,
                LogRequest = true,
                BufferContent = false,
                EnableHttpCompression = false,
                EnableKeepAlive = false
            };
            options.RequestHeaders.Add("trakt-api-version", "2");
            options.RequestHeaders.Add("trakt-api-key", TraktUris.Id);
            return options;
        }

        private async Task SetRequestHeaders(HttpRequestOptions options, TraktUser traktUser)
        {

            if (DateTime.Now > traktUser.AccessTokenExpiration)
            {
                traktUser.AccessToken = "";
            }
            if (string.IsNullOrEmpty(traktUser.AccessToken) || !string.IsNullOrEmpty(traktUser.PIN))
            {
                await RefreshUserAuth(traktUser).ConfigureAwait(false);
            }
            if (!string.IsNullOrEmpty(traktUser.AccessToken))
            {
                options.RequestHeaders.Add("Authorization", "Bearer " + traktUser.AccessToken);
            }

        }
    }
}
