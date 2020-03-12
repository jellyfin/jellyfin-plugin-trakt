using Trakt.Api.DataContracts.BaseModel;

namespace Trakt.Api.DataContracts.Users.Ratings
{
    public class TraktShowRated : TraktRated
    {
        public TraktShow show { get; set; }
    }
}