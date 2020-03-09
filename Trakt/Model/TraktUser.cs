using System;
using System.Collections.Generic;

namespace Trakt.Model
{
    public class TraktUser
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        public string LinkedMbUserId { get; set; }

        public bool UsesAdvancedRating { get; set; }

        public bool SkipUnwatchedImportFromTrakt { get; set; }

        public bool PostWatchedHistory { get; set; }

        public bool ExtraLogging { get; set; }

        public bool ExportMediaInfo { get; set; }

        public bool SynchronizeCollections { get; set; }

        public bool Scrobble { get; set; }

        public IReadOnlyList<string> LocationsExcluded { get; set; }

        public DateTime AccessTokenExpiration { get; set; }

        public TraktUser()
        {
            SkipUnwatchedImportFromTrakt = true;
            PostWatchedHistory = true;
            ExtraLogging = false;
            ExportMediaInfo = false;
            SynchronizeCollections = true;
            Scrobble = true;
        }
    }
}
