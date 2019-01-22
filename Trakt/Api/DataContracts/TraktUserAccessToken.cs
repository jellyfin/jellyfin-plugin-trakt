namespace Trakt.Api.DataContracts
{
    public class TraktUserAccessToken
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }
        public int created_at { get; set; }
    }
}