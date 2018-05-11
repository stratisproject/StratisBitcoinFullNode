using Newtonsoft.Json;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// Represents a connected peer.
    /// </summary>
    public class ConnectedPeerModel
    {
        /// <summary>A value indicating whether this peer is connected via an inbound or outbound connection.</summary>
        [JsonIgnore]
        public bool IsInbound { get; set; }

        /// <summary>The version this peer is running.</summary>
        public string Version { get; set; }

        /// <summary>The endpoint where this peer is located.</summary>
        public string RemoteSocketEndpoint { get; set; }

        /// <summary>The height of this connected peer's tip.</summary>
        public int TipHeight { get; set; }
    }
}
