using System.Collections.Generic;
using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync
{
    public class TraktSyncResponse
    {
        public Items added { get; set; }

        public Items deleted { get; set; }

        public Items existing { get; set; }

        public class Items
        {
            public int movies { get; set; }

            public int shows { get; set; }

            public int seasons { get; set; }

            public int episodes { get; set; }

            public int people { get; set; }
        }

        public NotFoundObjects not_found { get; set; }

        public class NotFoundObjects
        {
            public List<TraktMovie> movies { get; set; }

            public List<TraktShow> shows { get; set; }

            public List<TraktEpisode> episodes { get; set; }

            public List<TraktSeason> seasons { get; set; }

            public List<TraktPerson> people { get; set; }
        }
    }
}