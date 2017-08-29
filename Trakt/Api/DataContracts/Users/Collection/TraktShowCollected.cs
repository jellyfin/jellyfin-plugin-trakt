using System.Collections.Generic;

using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Collection
{
    
    public class TraktShowCollected
    {
        public string last_collected_at { get; set; }

        public TraktShow show { get; set; }

        public List<TraktSeasonCollected> seasons { get; set; }

        
        public class TraktSeasonCollected
        {
            public int number { get; set; }

            public List<TraktEpisodeCollected> episodes { get; set; }

            
            public class TraktEpisodeCollected
            {
                public int number { get; set; }

                public string collected_at { get; set; }

                public TraktMetadata metadata { get; set; }
            }
        }
    }
}