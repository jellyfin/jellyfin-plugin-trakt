
namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktSeasonId : TraktId
    {
        public int? tmdb { get; set; }

        public int? tvdb { get; set; }

        public int? tvrage { get; set; }
    }
}