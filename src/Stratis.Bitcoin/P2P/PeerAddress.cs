using System;
using System.Net;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// A class which holds data on a peer's (IPEndPoint) attempts, connections and successful handshake events.
    /// </summary>
    [JsonObject]
    public sealed class PeerAddress
    {
        /// <summary>
        /// The maximum amount of times a peer can be attempted within a give time frame.
        /// </summary>
        internal const int AttemptThreshold = 5;

        /// <summary>
        /// The maximum amount of times handshake can be attempted within a give time frame.
        /// </summary>
        internal const int AttemptHandshakeThreshold = 3;

        /// <summary>
        /// The amount of hours we will wait before selecting an attempted peer again,
        /// if it hasn't yet reached the <see cref="AttemptThreshold"/> amount of attempts.
        /// </summary>
        internal const int AttempThresholdHours = 1;

        /// <summary>
        /// The amount of hours after which the peer's failed connection attempts
        /// will be reset to zero.
        /// </summary>
        internal const int AttemptResetThresholdHours = 12;

        /// <summary>Endpoint of this peer.</summary>
        [JsonProperty(PropertyName = "endpoint")]
        [JsonConverter(typeof(IPEndPointConverter))]
        public IPEndPoint Endpoint { get; set; }

        /// <summary>Used to construct the <see cref="NetworkAddress"/> after deserializing this peer.</summary>
        [JsonProperty(PropertyName = "addressTime", NullValueHandling = NullValueHandling.Ignore)]
        private DateTimeOffset? addressTime;

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
        /// The amount of handshake attempts.
        /// <para>
        /// This gets reset when a handshake was successful.</para>
        /// </summary>
        [JsonIgnore]
        public int HandshakedAttempts { get; private set; }

        /// <summary>
        /// The last successful version handshake.
        /// <para>
        /// This is set when the connection attempt was successful and a handshake was done.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "lastConnectionHandshake", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? LastConnectionHandshake { get; private set; }

        /// <summary>
        /// The last handshake attempt.
        /// </summary>
        [JsonIgnore]
        public DateTimeOffset? LastHandshakeAttempt { get; private set; }

        /// <summary>
        /// The last time this peer was seen.
        /// <para>
        /// This is set via <see cref="Protocol.Behaviors.PingPongBehavior"/> to ensure that a peer is live.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "lastSeen", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastSeen { get; private set; }

        /// <summary>
        /// UTC DateTime when a peer is banned.
        /// </summary>
        /// <remarks>
        /// This is set in <see cref="PeerBanning"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "bantimestamp", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? BanTimeStamp { get; set; }

        /// <summary>
        /// UTC DateTime when the ban expires against the peer.
        /// </summary>
        /// <remarks>
        /// This is set in <see cref="PeerBanning"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "banuntil", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? BanUntil { get; set; }

        /// <summary>
        /// Reason for banning the peer.
        /// <remarks>
        /// This is set in <see cref="PeerBanning"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "banreason", NullValueHandling = NullValueHandling.Ignore)]
        public string BanReason { get; set; }

        /// <summary>
        /// Maintain a count of bad behaviour.
        /// <para>
        /// Once a certain score is reached ban the peer.
        /// </para>
        /// </summary>
        /// <remarks>
        /// The logic around this has not yet been implemented.
        /// This is set in <see cref="PeerBanning"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "banscore", NullValueHandling = NullValueHandling.Ignore)]
        public uint? BanScore { get; set; }

        /// <summary>
        /// <c>True</c> if the peer has had connection attempts but none successful.
        /// </summary>
        [JsonIgnore]
        public bool Attempted
        {
            get
            {
                return
                    (this.LastAttempt != null) &&
                    (this.LastConnectionSuccess == null) &&
                    (this.LastConnectionHandshake == null);
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
                    (this.LastAttempt == null) &&
                    (this.LastConnectionSuccess != null) &&
                    (this.LastConnectionHandshake == null);
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
                    (this.LastAttempt == null) &&
                    (this.LastConnectionSuccess == null) &&
                    (this.LastConnectionHandshake == null);
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
                    (this.LastAttempt == null) &&
                    (this.LastConnectionSuccess != null) &&
                    (this.LastConnectionHandshake != null);
            }
        }

        /// <summary>
        /// The last connection attempt.
        /// <para>
        /// This is set regardless of whether or not the connection attempt was successful or not.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "lastConnectionAttempt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? LastAttempt { get; private set; }

        /// <summary>
        /// The last successful connection attempt.
        /// <para>
        /// This is set when the connection attempt was successful (but not necessarily handshaked).
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "lastConnectionSuccess", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? LastConnectionSuccess { get; private set; }

        /// <summary>
        /// The last time this peer was discovered from.
        /// </summary>
        [JsonIgnore]
        public DateTime? LastDiscoveredFrom { get; private set; }

        /// <summary>
        /// Resets the amount of <see cref="ConnectionAttempts"/>.
        /// <para>
        /// This is reset when the amount of failed connection attempts reaches
        /// the <see cref="PeerAddress.AttemptThreshold"/> and the last attempt was
        /// made more than <see cref="PeerAddress.AttemptResetThresholdHours"/> ago.
        /// </para>
        /// </summary>
        internal void ResetAttempts()
        {
            this.ConnectionAttempts = 0;
        }

        /// <summary>
        /// Resets the amount of <see cref="HandshakedAttempts"/>.
        /// <para>
        /// This is reset when the amount of failed handshake attempts reaches
        /// the <see cref="PeerAddress.HandshakedAttempts"/> and the last attempt was
        /// made more than <see cref="PeerAddress.AttempThresholdHours"/> ago.
        /// </para>
        /// </summary>
        internal void ResetHandshakeAttempts()
        {
            this.HandshakedAttempts = 0;
        }

        /// <summary>
        /// Increments <see cref="ConnectionAttempts"/> and sets the <see cref="LastAttempt"/>.
        /// </summary>
        internal void SetAttempted(DateTime peerAttemptedAt)
        {
            this.ConnectionAttempts += 1;

            this.LastAttempt = peerAttemptedAt;
            this.LastConnectionSuccess = null;
            this.LastConnectionHandshake = null;
        }

        /// <summary>
        /// Increments <see cref="HandshakedAttempts"/> and sets the <see cref="LastHandshakeAttempt"/>.
        /// </summary>
        internal void SetHandshakeAttempted(DateTimeOffset handshakeAttemptedAt)
        {
            this.HandshakedAttempts += 1;
            this.LastHandshakeAttempt = handshakeAttemptedAt;
        }

        /// <summary>
        /// Sets the <see cref="LastConnectionSuccess"/>, <see cref="addressTime"/> and <see cref="NetworkAddress.Time"/> properties.
        /// <para>
        /// Resets <see cref="ConnectionAttempts"/> and <see cref="LastAttempt"/>.
        /// </para>
        /// </summary>
        internal void SetConnected(DateTimeOffset peerConnectedAt)
        {
            this.addressTime = peerConnectedAt;

            this.LastAttempt = null;
            this.ConnectionAttempts = 0;

            this.LastConnectionSuccess = peerConnectedAt;
        }

        /// <summary>Sets the <see cref="LastDiscoveredFrom"/> time.</summary>
        internal void SetDiscoveredFrom(DateTime lastDiscoveredFrom)
        {
            this.LastDiscoveredFrom = lastDiscoveredFrom;
        }

        /// <summary>Sets the <see cref="LastConnectionHandshake"/> date.</summary>
        internal void SetHandshaked(DateTimeOffset peerHandshakedAt)
        {
            this.ResetHandshakeAttempts();
            this.LastConnectionHandshake = peerHandshakedAt;
            this.LastHandshakeAttempt = null;
        }

        /// <summary>Sets the <see cref="LastSeen"/> date.</summary>
        internal void SetLastSeen(DateTime lastSeenAt)
        {
            this.LastSeen = lastSeenAt;
        }

        /// <summary>
        /// Un-bans a peer by resetting the <see cref="BanReason"/>, <see cref="BanScore"/>, <see cref="BanTimeStamp"/> and <see cref="BanUntil"/> properties.
        /// </summary>
        public void UnBan()
        {
            this.BanReason = null;
            this.BanScore = null;
            this.BanTimeStamp = null;
            this.BanUntil = null;
        }

        /// <summary>
        /// Creates a new peer address instance.
        /// </summary>
        /// <param name="endPoint">The end point of the peer.</param>
        public static PeerAddress Create(IPEndPoint endPoint)
        {
            return new PeerAddress
            {
                ConnectionAttempts = 0,
                Endpoint = endPoint.MapToIpv6(),
                loopback = IPAddress.Loopback.ToString()
            };
        }

        /// <summary>
        /// Creates a new peer address instance and sets the loopback address (source).
        /// </summary>
        /// <param name="endPoint">The end point of the peer.</param>
        /// <param name="loopback">The loopback (source) of the peer.</param>
        public static PeerAddress Create(IPEndPoint endPoint, IPAddress loopback)
        {
            PeerAddress peer = Create(endPoint);
            peer.loopback = loopback.ToString();
            return peer;
        }
    }
}