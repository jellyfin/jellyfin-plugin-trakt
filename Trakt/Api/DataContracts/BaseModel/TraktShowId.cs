
namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktShowId : TraktId
    {
        public string imdb { get; set; }

        public int? tmdb { get; set; }

        public int? tvdb { get; set; }

        public int? tvrage { get; set; }
    }
}