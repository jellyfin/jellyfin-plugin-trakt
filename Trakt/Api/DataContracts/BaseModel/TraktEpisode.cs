namespace Trakt.Api.DataContracts.BaseModel
{
    public class TraktEpisode
    {
        public int? season { get; set; }

        public int? number { get; set; }

        public string title { get; set; }

        public TraktEpisodeId ids { get; set; }
    }
}