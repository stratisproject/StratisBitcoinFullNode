using System;
using System.Net;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stratis.Bitcoin.P2P
{
    [JsonObject]
    public sealed class PeerAddress
    {
        private const int PeerAddressLastSeen = 30;
        private const int PeerMinimumFailDays = 7;
        private const int PeerMaximumWeeklyAttempts = 10;
        private const int PeerMaximumConnectionRetries = 3;

        #region Address Data

        [JsonProperty]
        private IPEndPoint endPoint;

        //[JsonProperty]
        //private string address;
        //[JsonProperty]
        //private int addressPort;
        [JsonProperty]
        private DateTimeOffset? addressTime;

        //[JsonIgnore]
        //public string AddressIP
        //{
        //    get { return this.address; }
        //}

        //[JsonIgnore]
        //public int AddressPort
        //{
        //    get { return this.addressPort; }
        //}

        [JsonIgnore]
        public NetworkAddress NetworkAddress
        {
            get
            {
                if (this.endPoint == null)
                    return null;

                var networkAddress = new NetworkAddress(this.endPoint);
                if (this.addressTime != null)
                    networkAddress.Time = this.addressTime.Value;

                return networkAddress;
            }
        }

        [JsonProperty]
        private string loopbackAddress;
        [JsonIgnore]
        public IPAddress Source
        {
            get
            {
                if (string.IsNullOrEmpty(this.loopbackAddress))
                    return null;
                return IPAddress.Parse(this.loopbackAddress);
            }
        }

        #endregion

        #region Connection Data

        [JsonProperty]
        private int connectionAttempts;
        [JsonIgnore]
        public int ConnectionAttempts
        {
            get { return this.connectionAttempts; }
        }

        [JsonProperty]
        private DateTimeOffset? lastConnectionHandshake;
        [JsonIgnore]
        public DateTimeOffset? LastConnectionHandshake
        {
            get { return this.lastConnectionHandshake; }
        }

        [JsonIgnore]
        public bool IsNew
        {
            get
            {
                return
                    this.lastConnectionAttempt == null &&
                    this.lastConnectionSuccess == null &&
                    this.lastConnectionHandshake == null;
            }
        }

        [JsonProperty]
        private DateTimeOffset? lastConnectionAttempt;
        [JsonIgnore]
        public DateTimeOffset? LastConnectionAttempt
        {
            get { return this.lastConnectionAttempt; }
        }

        [JsonProperty]
        private DateTimeOffset? lastConnectionSuccess;
        [JsonIgnore]
        public DateTimeOffset? LastConnectionSuccess
        {
            get { return this.lastConnectionSuccess; }
        }

        #endregion

        #region Connection Methods

        internal void Attempted(DateTimeOffset peerAttemptedAt)
        {
            this.connectionAttempts += 1;
            this.lastConnectionAttempt = peerAttemptedAt;
        }

        /// <summary>
        /// [NBitcoin] Do we need to throttle the update of lastSuccessfulConnect?
        /// https://github.com/stratisproject/NStratis/blob/2b0fbc3f6b809d92aaf43a8ee12f8baa724e5ccf/NBitcoin/Protocol/AddressManager.cs#L1014
        /// </summary>
        internal void Connected(DateTimeOffset peerConnectedAt)
        {
            this.addressTime = peerConnectedAt;
            this.NetworkAddress.Time = peerConnectedAt;

            this.lastConnectionAttempt = null;
            this.connectionAttempts = 0;

            this.lastConnectionSuccess = peerConnectedAt;
        }

        /// <summary>
        /// [NBitcoin] This replaces Good() method
        /// </summary>
        internal void Handshaked(DateTimeOffset peerHandshakedAt)
        {
            this.lastConnectionHandshake = peerHandshakedAt;
        }

        #endregion

        #region Peer Preference

        /// <summary>
        /// Determines whether the peer will be selected by <see cref="PeerConnector"/> when connecting.
        /// <para>
        /// <see cref="PeerHasNeverBeenConnectedTo"/> and <see cref="PeerHasBeenConnectedTo"/>.
        /// </para>
        /// </summary>
        [JsonIgnore]
        public bool Preferred
        {
            get
            {
                if (this.lastConnectionSuccess == null)
                    return this.PeerHasNeverBeenConnectedTo;

                return this.PeerHasBeenConnectedTo;
            }
        }

        /// <summary>
        /// Preference condition if the peer has never been connected to.
        /// <list>
        ///     <item>1: Prefer the peer if it is new (never attempted and never connected to).</item>
        ///     <item>2: The last connection attempt was more than 60 seconds ago.</item>
        ///     <item>3: The maximum number of retries has not been reached.</item>
        /// </list>
        /// </summary>
        [JsonIgnore]
        private bool PeerHasNeverBeenConnectedTo
        {
            get
            {
                if (this.lastConnectionAttempt == null)
                    return true;

                return
                    this.lastConnectionAttempt.Value >= DateTimeOffset.Now - TimeSpan.FromSeconds(60) &&
                    this.connectionAttempts < PeerMaximumConnectionRetries;
            }
        }


        /// <summary>
        /// Preference condition if the peer has been connected to.
        /// <list>
        ///     <item>1: The peer has been seen in the last 30 days..</item>
        ///     <item>2: The last connection successful connection was less than a week ago.</item>
        ///     <item>3: he maximum number of failures has not been reached.</item>
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
                    DateTimeOffset.Now - this.lastConnectionSuccess < TimeSpan.FromDays(PeerMinimumFailDays) &&
                    this.connectionAttempts < PeerMaximumWeeklyAttempts;
            }
        }

        #endregion

        #region Peer Selectability  

        /// <summary>
        /// Calculates the relative chance this peer should be given when selecting nodes to connect to.
        /// <para>
        /// This logic was taken from NBitcoin's implementation.
        /// </para>
        /// </summary>
        internal double Selectability
        {
            get
            {
                var currentDate = DateTimeOffset.Now;
                double selectability = 1.0;

                var timeSinceLastSeen = currentDate - this.NetworkAddress.Time;
                var timeSinceLastAttempt = currentDate - this.lastConnectionAttempt;

                if (timeSinceLastSeen < TimeSpan.Zero)
                    timeSinceLastSeen = TimeSpan.Zero;

                if (timeSinceLastAttempt < TimeSpan.Zero)
                    timeSinceLastAttempt = TimeSpan.Zero;

                // Deprioritize very recent attempts away.
                if (timeSinceLastAttempt < TimeSpan.FromSeconds(60 * 10))
                    selectability *= 0.01;

                // Deprioritize 66% after each failed attempt, but at most 1/28th 
                // to avoid the search taking forever or overly penalizing outages.
                selectability *= Math.Pow(0.66, Math.Min(this.connectionAttempts, 8));

                return selectability;
            }
        }

        #endregion

        public static PeerAddress Create(NetworkAddress address)
        {
            return new PeerAddress
            {
                //address = address.Endpoint.Address.ToString(),
                //addressPort = address.Endpoint.Port,
                connectionAttempts = 0,
                endPoint = address.Endpoint,
                loopbackAddress = IPAddress.Loopback.ToString(),
            };
        }

        public static PeerAddress Create(NetworkAddress address, IPAddress source)
        {
            var peer = Create(address);
            peer.loopbackAddress = source.ToString();
            return peer;
        }

        public bool Match(IPEndPoint endPoint)
        {
            return this.endPoint.Address.ToString() == endPoint.Address.ToString() && this.endPoint.Port == endPoint.Port;
        }
    }

    /// <summary>
    /// Converter used to convert <see cref="IPEndPoint"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public sealed class IPEndpointConverter : JsonConverter
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

            var endPointComponents = json.Split(':');
            return new IPEndPoint(IPAddress.Parse(endPointComponents[0]), Convert.ToInt32(endPointComponents[1]));
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address != null || ipEndPoint.Port != 0)
                {
                    JToken.FromObject(string.Format("{0}:{1}", ipEndPoint.Address, ipEndPoint.Port)).WriteTo(writer);
                    return;
                }
            }

            writer.WriteNull();
        }
    }
}