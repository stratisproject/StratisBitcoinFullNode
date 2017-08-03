using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    /// <summary>
    /// Data structure for RPC peer info nodes.
    /// </summary>
    public class PeerNodeModel
    {
        /// <summary>
        ///  Peer index.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public int Id { get; internal set; }

        /// <summary>
        /// The IP address and port of the peer.
        /// </summary>
        [JsonProperty(PropertyName = "addr")]
        public string Address { get; internal set; }

        /// <summary>
        /// Local address as reported by the peer.
        /// </summary>
        [JsonProperty(PropertyName = "addrlocal")]
        public string LocalAddress { get; internal set; }

        /// <summary>
        /// The services offered.
        /// </summary>
        [JsonProperty(PropertyName = "services")]
        public string Services { get; internal set; }

        /// <summary>
        /// The Unix epoch time of the last send from this node.
        /// Currently not populatated.
        /// </summary>
        [JsonProperty(PropertyName = "lastsend")]
        public int LastSend { get; internal set; }

        /// <summary>
        /// The Unix epoch time when we last received data from this node.
        /// Currently not populatated.
        /// </summary>
        [JsonProperty(PropertyName = "lastrecv")]
        public int LastReceive { get; internal set; }

        /// <summary>
        /// The total number of bytes we’ve sent to this node.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "bytessent")]
        public long BytesSent { get; internal set; }

        /// <summary>
        /// The total number of bytes we’ve received from this node.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "bytesrecv")]
        public long BytesReceived { get; internal set; }
        
        /// <summary>
        /// The connection time in seconds since epoch.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "conntime")]
        public int ConnectionTime { get; internal set; }

        /// <summary>
        /// The time offset in seconds.
        /// </summary>
        [JsonProperty(PropertyName = "timeoffset")]
        public int TimeOffset { get; internal set; }

        /// <summary>
        /// The protocol version number used by this node.
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        public uint Version { get; internal set; }

        /// <summary>
        /// The user agent this node sends in its version message.
        /// </summary>
        [JsonProperty(PropertyName = "subver")]
        public string SubVersion { get; internal set; }

        /// <summary>
        /// Whether node is inbound or outbound connection.
        /// </summary>
        [JsonProperty(PropertyName = "inbound")]
        public bool Inbound { get; internal set; }

        /// <summary>
        /// The starting height (block) of the peer.
        /// </summary>
        [JsonProperty(PropertyName = "startingheight")]
        public int StartingHeight { get; internal set; }
            
        /// <summary>
        /// The last header we have in common with this peer.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "synced_headers")]
        public int SynchronizedHeaders { get; internal set; }

        /// <summary>
        /// The last block we have in common with this peer.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "synced_blocks")]
        public int SynchronizedBlocks { get; internal set; }

        /// <summary>
        /// Whether the peer is whitelisted.
        /// </summary>
        [JsonProperty(PropertyName = "whitelisted")]
        public bool IsWhiteListed { get; internal set; }

        /// <summary>
        /// The heights of blocks we're currently asking from this peer.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "inflight")]
        public uint[] Inflight { get; internal set; }

        /// <summary>
        /// Number of blocks on peer.
        /// </summary>
        [JsonProperty(PropertyName = "blocks")]
        public int Blocks { get; internal set; }
    }
}
