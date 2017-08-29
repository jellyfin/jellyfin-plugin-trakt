using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Sync.Ratings
{
    public class TraktEpisodeRated : TraktRated
    {
        public int? number { get; set; }

        public TraktEpisodeId ids { get; set; }
    }
}