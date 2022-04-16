using System.Text.Json.Serialization;

namespace Trakt.Api.DataContracts
{
    /// <summary>
    /// The trakt.tv device code class.
    /// </summary>
    public class TraktDeviceCode
    {
        /// <summary>
        /// Gets or sets the device code.
        /// </summary>
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; }

        /// <summary>
        /// Gets or sets the user code.
        /// </summary>
        [JsonPropertyName("user_code")]
        public string UserCode { get; set; }

        /// <summary>
        /// Gets or sets the verification URL.
        /// </summary>
        [JsonPropertyName("verification_url")]
        public string VerificationUrl { get; set; }

        /// <summary>
        /// Gets or sets the expiration.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the interval.
        /// </summary>
        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }
}
