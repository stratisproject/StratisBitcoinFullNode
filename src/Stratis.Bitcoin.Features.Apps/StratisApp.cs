using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    /// <summary>
    /// Instances created from stratisApp.json
    /// </summary>
    public class StratisApp : IStratisApp
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
        
        public string Location { get; set; }

        [JsonProperty("webRoot")]
        public string WebRoot { get; set; } = "wwwroot";

        public string Address { get; set; }

        public bool IsSinglePageApp { get; set; } = true;
    }
}
