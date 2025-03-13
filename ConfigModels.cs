using Newtonsoft.Json;

namespace SingboxGUI
{
    // Root config class for deserialization
    public class RootConfig
    {
        public ExperimentalConfig Experimental { get; set; }
    }

    // Experimental configuration section
    public class ExperimentalConfig
    {
        [JsonProperty("cache_file")]
        public CacheFileConfig CacheFile { get; set; }

        [JsonProperty("clash_api")]
        public ClashApiConfig ClashApi { get; set; }
    }

    // Clash API configuration
    public class ClashApiConfig
    {
        [JsonProperty("external_controller")]
        public string ExternalController { get; set; }

        [JsonProperty("access_control_allow_origin")]
        public string AccessControlAllowOrigin { get; set; }

        [JsonProperty("access_control_allow_private_network")]
        public bool AccessControlAllowPrivateNetwork { get; set; }
    }

    // Cache file configuration
    public class CacheFileConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("cache_id")]
        public string CacheId { get; set; }
    }
}