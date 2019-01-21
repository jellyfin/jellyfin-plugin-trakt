

namespace Trakt.Api.DataContracts
{
    
    public class TraktDeviceCode
    {
        public string device_code { get; set; }
        public string user_code { get; set; }
        public string verification_url { get; set; }
        public int expires_in { get; set; }
        public int interval { get; set; }
    }
}