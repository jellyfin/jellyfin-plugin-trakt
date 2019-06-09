using System;
using System.Threading.Tasks;

namespace Trakt.Model
{
    public class TraktUser
    {        
        public String AccessToken { get; set; }

        public String RefreshToken { get; set; }

        public String LinkedMbUserId { get; set; }

        public bool UsesAdvancedRating { get; set; }

        public bool SkipUnwatchedImportFromTrakt { get; set; }

        public bool PostWatchedHistory { get; set; }

        public bool ExtraLogging { get; set; }

        public bool ExportMediaInfo { get; set; }

        public bool SynchronizeCollections { get; set; }

        public bool Scrobble { get; set; }

        public String[] LocationsExcluded { get; set; }
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
