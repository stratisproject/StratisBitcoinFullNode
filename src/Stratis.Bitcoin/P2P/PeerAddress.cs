using System;
using System.Net;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// A class which holds data on a peer's (IPEndPoint) attempts, connections and successful handshake events.
    /// </summary>
    [JsonObject]
    public sealed class PeerAddress
    {
        private const int PeerAddressLastSeen = 30;

        private const int PeerMinimumFailDays = 7;

        private const int PeerMaximumWeeklyAttempts = 10;

        private const int PeerMaximumConnectionRetries = 3;

        /// <summary>EndPoint of this peer.</summary>
        [JsonProperty(PropertyName = "endpoint")]
        [JsonConverter(typeof(IPEndPointConverter))]
        private IPEndPoint endpoint;

        /// <summary>Used to construct the <see cref="NetworkAddress"/> after deserializing this peer.</summary>
        [JsonProperty(PropertyName = "addressTime", NullValueHandling = NullValueHandling.Ignore)]
        private DateTimeOffset? addressTime;

        /// <summary>The <see cref="NetworkAddress"/> of this peer.</summary>
        [JsonIgnore]
        public NetworkAddress NetworkAddress
        {
            get
            {
                if (this.endpoint == null)
                    return null;

                var networkAddress = new NetworkAddress(this.endpoint);
                if (this.addressTime != null)
                    networkAddress.Time = this.addressTime.Value;

                return networkAddress;
            }
        }

        /// <summary>The source address of this peer.</summary>
        [JsonProperty(PropertyName = "loopback")]
        private string loopback;

        [JsonIgnore]
        public IPAddress Loopback
        {
            get
            {
                if (string.IsNullOrEmpty(this.loopback))
                    return null;
                return IPAddress.Parse(this.loopback);
            }
        }

        /// <summary>
        /// The amount of connection attempts.
        /// <para>
        /// This gets reset when a connection was successful.</para>
        /// </summary>
        [JsonProperty(PropertyName = "connectionAttempts")]
        public int ConnectionAttempts { get; private set; }

        /// <summary>
        /// The last successful version handshake.
        /// <para>
        /// This is set when the connection attempt was successful and a handshake was done.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "lastConnectionHandshake", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? LastConnectionHandshake { get; private set; }

        /// <summary>
        /// <c>True</c> if the peer has had connection attempts but none successful.
        /// </summary>
        [JsonIgnore]
        public bool Attempted
        {
            get
            {
                return
                    this.LastConnectionAttempt != null &&
                    this.LastConnectionSuccess == null;
            }
        }

        /// <summary>
        /// <c>True</c> if the peer has had a successful connection attempt.
        /// </summary>
        [JsonIgnore]
        public bool Connected
        {
            get
            {
                return
                    this.LastConnectionAttempt == null &&
                    this.LastConnectionSuccess != null &&
                    this.LastConnectionHandshake == null;
            }
        }

        /// <summary>
        /// <c>True</c> if the peer has never had connection attempts.
        /// </summary>
        [JsonIgnore]
        public bool Fresh
        {
            get
            {
                return
                    this.LastConnectionAttempt == null &&
                    this.LastConnectionSuccess == null &&
                    this.LastConnectionHandshake == null;
            }
        }

        /// <summary>
        /// <c>True</c> if the peer has had a successful connection attempt and handshaked.
        /// </summary>
        [JsonIgnore]
        public bool Handshaked
        {
            get
            {
                return
                    this.LastConnectionAttempt == null &&
                    this.LastConnectionSuccess == null &&
                    this.LastConnectionHandshake != null;
            }
        }

        /// <summary>
        /// The last connection attempt.
        /// <para>
        /// This is set regardless of whether or not the connection attempt was successful or not.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "lastConnectionAttempt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? LastConnectionAttempt { get; private set; }

        /// <summary>
        /// The last successful connection attempt.
        /// <para>
        /// This is set when the connection attempt was successful (but not necessarily handshaked).
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "lastConnectionSuccess", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? LastConnectionSuccess { get; private set; }

        /// <summary>
        /// Increments <see cref="ConnectionAttempts"/> and sets the <see cref="LastConnectionAttempt"/>.
        /// </summary>
        internal void SetAttempted(DateTimeOffset peerAttemptedAt)
        {
            this.ConnectionAttempts += 1;
            this.LastConnectionAttempt = peerAttemptedAt;
            this.LastConnectionSuccess = null;
            this.LastConnectionHandshake = null;
        }

        /// <summary>
        /// Sets the <see cref="LastConnectionSuccess"/>, <see cref="addressTime"/> and <see cref="NetworkAddress.Time"/> properties.
        /// <para>
        /// Resets <see cref="ConnectionAttempts"/> and <see cref="LastConnectionAttempt"/>.
        /// </para>
        /// <para>
        /// TODO: [NBitcoin] Do we need to throttle the update of lastSuccessfulConnect?
        /// https://github.com/stratisproject/NStratis/blob/2b0fbc3f6b809d92aaf43a8ee12f8baa724e5ccf/NBitcoin/Protocol/AddressManager.cs#L1014
        /// </para>
        /// </summary>
        internal void SetConnected(DateTimeOffset peerConnectedAt)
        {
            this.addressTime = peerConnectedAt;
            this.NetworkAddress.Time = peerConnectedAt;

            this.LastConnectionAttempt = null;
            this.ConnectionAttempts = 0;

            this.LastConnectionSuccess = peerConnectedAt;
        }

        /// <summary>Sets the <see cref="LastConnectionHandshake"/> date.</summary>
        internal void SetHandshaked(DateTimeOffset peerHandshakedAt)
        {
            this.LastConnectionSuccess = null;
            this.LastConnectionHandshake = peerHandshakedAt;
        }

        /// <summary>
        /// Determines whether the peer will be selected by the <see cref="IPeerConnector"/> when connecting.
        /// </summary>
        /// <seealso cref="PeerHasNeverBeenConnectedTo"/>
        /// <seealso cref="PeerHasBeenConnectedTo"/>
        [JsonIgnore]
        public bool Preferred
        {
            get
            {
                if (this.LastConnectionSuccess == null)
                    return this.PeerHasNeverBeenConnectedTo;

                return this.PeerHasBeenConnectedTo;
            }
        }

        /// <summary>
        /// Preference condition if the peer has never been connected to.
        /// <list>
        /// <item>1: Prefer the peer if it is new (never attempted and never connected to).</item>
        /// <item>2: The last connection attempt was more than 60 seconds ago.</item>
        /// <item>3: The maximum number of retries has not been reached.</item>
        /// </list>
        /// </summary>
        [JsonIgnore]
        private bool PeerHasNeverBeenConnectedTo
        {
            get
            {
                if (this.LastConnectionAttempt == null)
                    return true;

                return
                    this.LastConnectionAttempt.Value >= DateTimeOffset.Now - TimeSpan.FromSeconds(60) &&
                    this.ConnectionAttempts < PeerMaximumConnectionRetries;
            }
        }

        /// <summary>
        /// Preference condition if the peer has been connected to.
        /// <list>
        /// <item>1: The peer has been seen in the last 30 days..</item>
        /// <item>2: The last connection successful connection was less than a week ago.</item>
        /// <item>3: The maximum number of failures has not been reached.</item>
        /// </list>
        /// </summary>
        [JsonIgnore]
        private bool PeerHasBeenConnectedTo
        {
            get
            {
                if (DateTimeOffset.Now - this.NetworkAddress.Time > TimeSpan.FromDays(PeerAddressLastSeen))
                    return false;

                return
                    DateTimeOffset.Now - this.LastConnectionSuccess < TimeSpan.FromDays(PeerMinimumFailDays) &&
                    this.ConnectionAttempts < PeerMaximumWeeklyAttempts;
            }
        }

        /// <summary>
        /// Calculates the relative chance this peer should be given when selecting nodes to connect to.
        /// <para>
        /// This logic was taken from NBitcoin's implementation.
        /// </para>
        /// <para>
        /// We effectively "deprioritize" the peer away after each failed attempt,
        /// making it harder for the peer to be able to be selected by the
        /// address manager. But at most no more than 1/28th to avoid the search taking forever
        /// or overly penalizing outages.
        /// </para>
        /// </summary>
        internal double Selectability
        {
            get
            {
                double selectability = 1.0;

                var timeSinceLastAttempt = DateTimeOffset.Now - this.LastConnectionAttempt;
                if (timeSinceLastAttempt < TimeSpan.Zero)
                    timeSinceLastAttempt = TimeSpan.Zero;

                // If the last attempt was less than 10 minutes away,
                // deprioritize the peer by 10%.
                if (timeSinceLastAttempt < TimeSpan.FromMinutes(10))
                    selectability *= 0.01;

                // Deprioritize 66% after each failed attempt, but at most 1/28th
                // to avoid the search taking forever or overly penalizing outages.
                selectability *= Math.Pow(0.66, Math.Min(this.ConnectionAttempts, 8));

                return selectability;
            }
        }

        /// <summary>
        /// Creates a new peer address instance.
        /// </summary>
        /// <param name="address">The network address of the peer.</param>
        public static PeerAddress Create(NetworkAddress address)
        {
            return new PeerAddress
            {
                ConnectionAttempts = 0,
                endpoint = address.Endpoint,
                loopback = IPAddress.Loopback.ToString()
            };
        }

        /// <summary>
        /// Creates a new peer address instance and sets the loopback address (source).
        /// </summary>
        /// <param name="address">The network address of the peer.</param>
        /// <param name="loopback">The loopback (source) of the peer.</param>
        public static PeerAddress Create(NetworkAddress address, IPAddress loopback)
        {
            var peer = Create(address);
            peer.loopback = loopback.ToString();
            return peer;
        }
    }

    /// <summary>
    /// Converter used to convert <see cref="IPEndPoint"/> to and from JSON.
    /// </summary>
    /// <seealso cref="JsonConverter" />
    public sealed class IPEndPointConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IPEndPoint);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var json = JToken.Load(reader).ToString();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var endPointComponents = json.Split('|');
            return new IPEndPoint(IPAddress.Parse(endPointComponents[0]), Convert.ToInt32(endPointComponents[1]));
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address != null || ipEndPoint.Port != 0)
                {
                    JToken.FromObject(string.Format("{0}|{1}", ipEndPoint.Address, ipEndPoint.Port)).WriteTo(writer);
                    return;
                }
            }

            writer.WriteNull();
        }
    }
}
