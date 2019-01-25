using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Services;
using Microsoft.Extensions.Logging;
using Trakt.Helpers;
using Trakt.Model;

namespace Trakt.Api
{
    [Route("/Trakt/Users/{UserId}/Authorize", "POST")]
    public class DeviceAuthorization
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }
    }

    [Route("/Trakt/Users/{UserId}/PollAuthorizationStatus", "GET")]
    public class PollAuthorizationStatus
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }
    }

    [Route("/Trakt/Users/{UserId}/Items/{Id}/Rate", "POST")]
    public class RateItem
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }

        [ApiMember(Name = "Id", Description = "Item Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string Id { get; set; }

        [ApiMember(Name = "Rating", Description = "Rating between 1 - 10 (0 = unrate)", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int Rating { get; set; }
        
    }

    [Route("/Trakt/Users/{UserId}/RecommendedMovies", "POST")]
    public class RecommendedMovies
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }

        [ApiMember(Name = "Genre", Description = "Genre slug to filter by. (See http://trakt.tv/api-docs/genres-movies)", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public int Genre { get; set; }

        [ApiMember(Name = "StartYear", Description = "4-digit year to filter movies released this year or later", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int StartYear { get; set; }

        [ApiMember(Name = "EndYear", Description = "4-digit year to filter movies released this year or earlier", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int EndYear { get; set; }

        [ApiMember(Name = "HideCollected", Description = "Set true to hide movies in the users collection", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool HideCollected { get; set; }

        [ApiMember(Name = "HideWatchlisted", Description = "Set true to hide movies in the users watchlist", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool HideWatchlisted { get; set; }
    }

    [Route("/Trakt/Users/{UserId}/RecommendedShows", "POST")]
    public class RecommendedShows
    {
        [ApiMember(Name = "UserId", Description = "User Id", IsRequired = true, DataType = "string", ParameterType = "path", Verb = "POST")]
        public string UserId { get; set; }

        [ApiMember(Name = "Genre", Description = "Genre slug to filter by. (See http://trakt.tv/api-docs/genres-shows)", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
        public int Genre { get; set; }

        [ApiMember(Name = "StartYear", Description = "4-digit year to filter shows released this year or later", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int StartYear { get; set; }

        [ApiMember(Name = "EndYear", Description = "4-digit year to filter shows released this year or earlier", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "POST")]
        public int EndYear { get; set; }

        [ApiMember(Name = "HideCollected", Description = "Set true to hide shows in the users collection", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool HideCollected { get; set; }

        [ApiMember(Name = "HideWatchlisted", Description = "Set true to hide shows in the users watchlist", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
        public bool HideWatchlisted { get; set; }
    }



    /// <summary>
    /// 
    /// </summary>
    public class TraktUriService : IService
    {
        private readonly TraktApi _traktApi;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraktUriService"/> class.
        /// </summary>
        /// <param name="traktApi">The trakt API.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="libraryManager">The library manager.</param>
        public TraktUriService(TraktApi traktApi, ILogger logger, ILibraryManager libraryManager)
        {
            _traktApi = traktApi;
            _logger = logger;
            _libraryManager = libraryManager;
        }


        public object Post(DeviceAuthorization deviceAuthorizationRequest)
        {
            _logger.LogInformation("DeviceAuthorization request received");

            // Create a user if we don't have one yet - TODO there should be an endpoint for this that creates a default user
            var traktUser = UserHelper.GetTraktUser(deviceAuthorizationRequest.UserId);
            if (traktUser == null)
            {
                Plugin.Instance.PluginConfiguration.AddUser(deviceAuthorizationRequest.UserId);
                traktUser = UserHelper.GetTraktUser(deviceAuthorizationRequest.UserId);
                Plugin.Instance.SaveConfiguration();
            }
            string userCode = _traktApi.AuthorizeDevice(traktUser);

            return new
            {
                userCode
            };
        }
        public object Get(PollAuthorizationStatus pollRequest)
        {
            _logger.LogInformation("PollAuthorizationStatus request received");
            var traktUser = UserHelper.GetTraktUser(pollRequest.UserId);
            bool isAuthorized = traktUser.AccessToken != null && traktUser.RefreshToken != null;

            if (Plugin.Instance.PollingTasks.TryGetValue(pollRequest.UserId, out var task))
            {
                isAuthorized = task.Result;
                Plugin.Instance.PollingTasks.Remove(pollRequest.UserId);
            }

            return new
            {
                isAuthorized
            };
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public object Post(RateItem request)
        {
            _logger.LogInformation("RateItem request received");

            var currentItem = _libraryManager.GetItemById(new Guid(request.Id));

            if (currentItem == null)
            {
                _logger.LogInformation("currentItem is null");
                return null;
            }

            return _traktApi.SendItemRating(currentItem, request.Rating, UserHelper.GetTraktUser(request.UserId)).Result;
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public object Post(RecommendedMovies request)
        {
            return _traktApi.SendMovieRecommendationsRequest(UserHelper.GetTraktUser(request.UserId)).Result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public object Post(RecommendedShows request)
        {
            return _traktApi.SendShowRecommendationsRequest(UserHelper.GetTraktUser(request.UserId)).Result;
        }
    }
}
