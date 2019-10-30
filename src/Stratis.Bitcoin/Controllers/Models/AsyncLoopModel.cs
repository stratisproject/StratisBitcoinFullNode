using Newtonsoft.Json;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// A class representing an async loop and its current status.
    /// </summary>
    public class AsyncLoopModel
    {
        /// <summary>
        /// The name of the loop.
        /// </summary>
        [JsonProperty(PropertyName = "loopName")]
        public string LoopName { get; set; }

        /// <summary>
        /// The loop's status.
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }
    }
}