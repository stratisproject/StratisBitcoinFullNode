using Newtonsoft.Json;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// Data structure for connected peer node.
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

        /// <summary>
        /// Whether the peer has asked us to relay transactions to it.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "relaytxes")]
        public bool IsRelayTransactions { get; internal set; }

        ///  <summary>
        ///  The Unix epoch time of the last send from this node.
        /// Currently not populated.
        ///  </summary>
        [JsonProperty(PropertyName = "lastsend")]
        public int LastSend { get; internal set; }

        ///  <summary>
        ///  The Unix epoch time when we last received data from this node.
        /// Currently not populated.
        ///  </summary>
        [JsonProperty(PropertyName = "lastrecv")]
        public int LastReceive { get; internal set; }

        ///  <summary>
        ///  The total number of bytes we’ve sent to this node.
        ///  Currently not populated.
        ///  </summary>
        [JsonProperty(PropertyName = "bytessent")]
        public long BytesSent { get; internal set; }

        ///  <summary>
        ///  The total number of bytes we’ve received from this node.
        ///  Currently not populated.
        ///  </summary>
        [JsonProperty(PropertyName = "bytesrecv")]
        public long BytesReceived { get; internal set; }

        ///  <summary>
        ///  The connection time in seconds since epoch.
        ///  Currently not populated.
        ///  </summary>
        [JsonProperty(PropertyName = "conntime")]
        public int ConnectionTime { get; internal set; }

        /// <summary>
        /// The time offset in seconds.
        /// </summary>
        [JsonProperty(PropertyName = "timeoffset")]
        public int TimeOffset { get; internal set; }

        /// <summary>
        /// The ping time to the node in seconds.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "pingtime")]
        public double PingTime { get; internal set; }

        /// <summary>
        /// The minimum observed ping time.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "minping")]
        public double MinPing { get; internal set; }

        /// <summary>
        /// The number of seconds waiting for a ping.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "pingwait")]
        public double PingWait { get; internal set; }

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
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "inbound")]
        public bool Inbound { get; internal set; }

        /// <summary>
        /// Whether connection was due to addnode.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "addnode")]
        public bool IsAddNode { get; internal set; }

        /// <summary>
        /// The starting height (block) of the peer.
        /// </summary>
        [JsonProperty(PropertyName = "startingheight")]
        public int StartingHeight { get; internal set; }

        /// <summary>
        /// The ban score for the node.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "banscore")]
        public int BanScore { get; internal set; }

        ///  <summary>
        ///  The last header we have in common with this peer.
        ///  Currently not populated.
        ///  </summary>
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
        /// Total sent bytes aggregated by message type.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "bytessent_per_msg")]
        public uint[] BytesSentPerMessage { get; internal set; }

        /// <summary>
        /// Total received bytes aggregated by message type.
        /// Currently not populated.
        /// </summary>
        [JsonProperty(PropertyName = "bytesrecv_per_msg")]
        public uint[] BytesReceivedPerMessage { get; internal set; }
    }
}
