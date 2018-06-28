using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Threading;
using Trakt.Api;
using Trakt.Model;

namespace Trakt.Helpers
{
    internal class LibraryManagerEventsHelper
    {
        private readonly List<LibraryEvent> _queuedEvents;
        private ITimer _queueTimer;
        private readonly ILogger _logger;
        private readonly TraktApi _traktApi;
        private readonly ITimerFactory _timerFactory;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="traktApi"></param>
        public LibraryManagerEventsHelper(ILogger logger, TraktApi traktApi, ITimerFactory timerFactory)
        {
            _queuedEvents = new List<LibraryEvent>();
            _logger = logger;
            _traktApi = traktApi;
            _timerFactory = timerFactory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="eventType"></param>
        public void QueueItem(BaseItem item, EventType eventType)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            if (_queueTimer == null)
            {
                _queueTimer = _timerFactory.Create(OnQueueTimerCallback, null, TimeSpan.FromMilliseconds(20000),
                    Timeout.InfiniteTimeSpan);
            }
            else
            {
                _queueTimer.Change(TimeSpan.FromMilliseconds(20000), Timeout.InfiniteTimeSpan);
            }

            var users = Plugin.Instance.PluginConfiguration.TraktUsers;

            if (users == null || users.Length == 0) return;

            // we need to process the video for each user
            foreach (var user in users.Where(x => _traktApi.CanSync(item, x)))
            {
                // we have a match, this user is watching the folder the video is in. Add to queue and they
                // will be processed when the next timer elapsed event fires.
                var libraryEvent = new LibraryEvent { Item = item, TraktUser = user, EventType = eventType };
                _queuedEvents.Add(libraryEvent);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        private async void OnQueueTimerCallback(object state)
        {
            try
            {
                await OnQueueTimerCallbackInternal().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error in OnQueueTimerCallbackInternal", ex);
            }
        }

        private async Task OnQueueTimerCallbackInternal()
        {
            _logger.Info("Timer elapsed - Processing queued items");

            if (!_queuedEvents.Any())
            {
                _logger.Info("No events... Stopping queue timer");
                // This may need to go
                return;
            }
            var queue = _queuedEvents.ToList();
            _queuedEvents.Clear();
            foreach (var traktUser in Plugin.Instance.PluginConfiguration.TraktUsers)
            {
                var queuedMovieDeletes = queue.Where(ev =>
                    new Guid(ev.TraktUser.LinkedMbUserId).Equals(new Guid(traktUser.LinkedMbUserId)) &&
                    ev.Item is Movie &&
                    ev.EventType == EventType.Remove).ToList();

                if (queuedMovieDeletes.Any())
                {
                    _logger.Info(queuedMovieDeletes.Count + " Movie Deletes to Process");
                    await ProcessQueuedMovieEvents(queuedMovieDeletes, traktUser, EventType.Remove).ConfigureAwait(false);
                }
                else
                {
                    _logger.Info("No Movie Deletes to Process");
                }

                var queuedMovieAdds = queue.Where(ev =>
                    new Guid(ev.TraktUser.LinkedMbUserId).Equals(new Guid(traktUser.LinkedMbUserId)) &&
                    ev.Item is Movie &&
                    ev.EventType == EventType.Add).ToList();

                if (queuedMovieAdds.Any())
                {
                    _logger.Info(queuedMovieAdds.Count + " Movie Adds to Process");
                    await ProcessQueuedMovieEvents(queuedMovieAdds, traktUser, EventType.Add).ConfigureAwait(false);
                }
                else
                {
                    _logger.Info("No Movie Adds to Process");
                }

                var queuedEpisodeDeletes = queue.Where(ev =>
                    new Guid(ev.TraktUser.LinkedMbUserId).Equals(new Guid(traktUser.LinkedMbUserId)) &&
                    ev.Item is Episode &&
                    ev.EventType == EventType.Remove).ToList();

                if (queuedEpisodeDeletes.Any())
                {
                    _logger.Info(queuedEpisodeDeletes.Count + " Episode Deletes to Process");
                    await ProcessQueuedEpisodeEvents(queuedEpisodeDeletes, traktUser, EventType.Remove).ConfigureAwait(false);
                }
                else
                {
                    _logger.Info("No Episode Deletes to Process");
                }

                var queuedEpisodeAdds = queue.Where(ev =>
                    new Guid(ev.TraktUser.LinkedMbUserId).Equals(new Guid(traktUser.LinkedMbUserId)) &&
                    ev.Item is Episode &&
                    ev.EventType == EventType.Add).ToList();

                if (queuedEpisodeAdds.Any())
                {
                    _logger.Info(queuedEpisodeAdds.Count + " Episode Adds to Process");
                    await ProcessQueuedEpisodeEvents(queuedEpisodeAdds, traktUser, EventType.Add).ConfigureAwait(false);
                }
                else
                {
                    _logger.Info("No Episode Adds to Process");
                }

                var queuedShowDeletes = queue.Where(ev =>
                    new Guid(ev.TraktUser.LinkedMbUserId).Equals(new Guid(traktUser.LinkedMbUserId)) &&
                    ev.Item is Series &&
                    ev.EventType == EventType.Remove).ToList();

                if (queuedShowDeletes.Any())
                {
                    _logger.Info(queuedMovieDeletes.Count + " Series Deletes to Process");
                    await ProcessQueuedShowEvents(queuedShowDeletes, traktUser, EventType.Remove).ConfigureAwait(false);
                }
                else
                {
                    _logger.Info("No Series Deletes to Process");
                }
            }

            // Everything is processed. Reset the event list.
            _queuedEvents.Clear();
        }

        private async Task ProcessQueuedShowEvents(IEnumerable<LibraryEvent> events, TraktUser traktUser, EventType eventType)
        {
            var shows = events.Select(lev => (Series)lev.Item)
                .Where(lev => !string.IsNullOrEmpty(lev.Name) && !string.IsNullOrEmpty(lev.GetProviderId(MetadataProviders.Tvdb)))
                .ToList();
            try
            {
                // Should probably not be awaiting this, but it's unlikely a user will be deleting more than one or two shows at a time
                foreach (var show in shows)
                    await _traktApi.SendLibraryUpdateAsync(show, traktUser, CancellationToken.None, eventType).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Exception handled processing queued series events", ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="events"></param>
        /// <param name="traktUser"></param>
        /// <param name="eventType"></param>
        /// <returns></returns>
        private async Task ProcessQueuedMovieEvents(IEnumerable<LibraryEvent> events, TraktUser traktUser, EventType eventType)
        {
            var movies = events.Select(lev => (Movie)lev.Item)
                .Where(lev => !string.IsNullOrEmpty(lev.Name) && !string.IsNullOrEmpty(lev.GetProviderId(MetadataProviders.Imdb)))
                .ToList();
            try
            {
                await _traktApi.SendLibraryUpdateAsync(movies, traktUser, CancellationToken.None, eventType).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Exception handled processing queued movie events", ex);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="events"></param>
        /// <param name="traktUser"></param>
        /// <param name="eventType"></param>
        /// <returns></returns>
        private async Task ProcessQueuedEpisodeEvents(IEnumerable<LibraryEvent> events, TraktUser traktUser, EventType eventType)
        {
            var episodes = events.Select(lev => (Episode)lev.Item)
                .Where(lev => lev.Series != null && (!string.IsNullOrEmpty(lev.Series.Name) && !string.IsNullOrEmpty(lev.Series.GetProviderId(MetadataProviders.Tvdb))))
                .OrderBy(i => i.Series.Id)
                .ToList();

            // Can't progress further without episodes
            if (!episodes.Any())
            {
                _logger.Info("episodes count is 0");

                return;
            }

            var payload = new List<Episode>();
            var currentSeriesId = episodes[0].Series.Id;

            foreach (var ep in episodes)
            {
                if (!currentSeriesId.Equals(ep.Series.Id))
                {
                    // We're starting a new series. Time to send the current one to trakt.tv
                    await _traktApi.SendLibraryUpdateAsync(payload, traktUser, CancellationToken.None, eventType);

                    currentSeriesId = ep.Series.Id;
                    payload.Clear();
                }

                payload.Add(ep);
            }

            if (payload.Any())
            {
                try
                {
                    await _traktApi.SendLibraryUpdateAsync(payload, traktUser, CancellationToken.None, eventType);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Exception handled processing queued episode events", ex);
                }
            }
        }
    }

    #region internal helper types

    internal class LibraryEvent
    {
        public BaseItem Item { get; set; }
        public TraktUser TraktUser { get; set; }
        public EventType EventType { get; set; }
    }

    public enum EventType
    {
        Add,
        Remove,
        Update
    }

    #endregion
}
