namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktEpisodeId : TraktId
    {
        public string imdb { get; set; }

        public int? tmdb { get; set; }

        public int? tvdb { get; set; }

        public int? tvrage { get; set; }
    }
}