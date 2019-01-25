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
        
        // Expiration can be a bit of a problem with Trakt. It's usually 90 days, but it's unclear when the
        // refresh_token expires, so leave a little buffer for expiration...
        public int expirationWithBuffer => expires_in * 3 / 4;
    }
}