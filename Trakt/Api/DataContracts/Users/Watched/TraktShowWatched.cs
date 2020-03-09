using System.Collections.Generic;

using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Watched
{
    public class TraktShowWatched
    {
        public int plays { get; set; }

        public string last_watched_at { get; set; }

        public TraktShow show { get; set; }

        public List<Season> seasons { get; set; }

        public class Season
        {
            public int number { get; set; }

            public List<Episode> episodes { get; set; }

            public class Episode
            {
                public int number { get; set; }

                public int plays { get; set; }
            }
        }
    }
}