using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P.Protocol.Behaviors
{
    [Flags]
    public enum PingPongMode
    {
        SendPing = 1,
        RespondPong = 2,
        Both = 3
    }

    /// <summary>
    /// The PingPongBehavior is responsible for firing ping message every PingInterval and responding with pong message, and close the connection if the Ping has not been completed after TimeoutInterval.
    /// </summary>
    public class PingPongBehavior : NetworkPeerBehavior
    {
        private object cs = new object();

        private PingPongMode mode;

        /// <summary>
        /// Whether the behavior send Ping and respond with Pong (Default : Both)
        /// </summary>
        public PingPongMode Mode
        {
            get
            {
                return this.mode;
            }
            set
            {
                this.AssertNotAttached();
                this.mode = value;
            }
        }

        private TimeSpan timeoutInterval;

        /// <summary>
        /// Interval after which an unresponded Ping will result in a disconnection. (Default : 20 minutes)
        /// </summary>
        public TimeSpan TimeoutInterval
        {
            get
            {
                return this.timeoutInterval;
            }
            set
            {
                this.AssertNotAttached();
                this.timeoutInterval = value;
            }
        }

        private TimeSpan pingInterval;

        /// <summary>
        /// Interval after which a Ping message is fired after the last received Pong (Default : 2 minutes)
        /// </summary>
        public TimeSpan PingInterval
        {
            get
            {
                return this.pingInterval;
            }
            set
            {
                this.AssertNotAttached();
                this.pingInterval = value;
            }
        }
        
        public PingPongBehavior()
        {
            this.Mode = PingPongMode.Both;
            this.TimeoutInterval = TimeSpan.FromMinutes(20.0); // Long time, if in middle of download of a large bunch of blocks, it can takes time.
            this.PingInterval = TimeSpan.FromMinutes(2.0);
        }

        protected override void AttachCore()
        {
            if ((this.AttachedPeer.PeerVersion != null) && !this.PingVersion()) // If not handshaked, still attach (the callback will also check version).
                return;

            this.SubscribeToPayload<PingPayload>(this.ProcessPingPayloadAsync);
            this.SubscribeToPayload<PongPayload>(this.ProcessPongPayloadAsync);
            
            this.RegisterDisposable(new Timer(this.Ping, null, 0, (int)this.PingInterval.TotalMilliseconds));
        }

        private async Task ProcessPingPayloadAsync(PingPayload payload, INetworkPeer networkPeer)
        {
            if (!this.PingVersion())
                return;

            if (this.Mode.HasFlag(PingPongMode.RespondPong))
            {
                try
                {
                    await networkPeer.SendMessageAsync(new PongPayload()
                    {
                        Nonce = payload.Nonce
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private async Task ProcessPongPayloadAsync(PongPayload payload, INetworkPeer networkPeer)
        {
            if (!this.PingVersion())
                return;

            if (this.Mode.HasFlag(PingPongMode.SendPing) && (this.currentPing != null) && (this.currentPing.Nonce == payload.Nonce))
            {
                this.Latency = DateTimeOffset.UtcNow - this.dateSent;
                this.ClearCurrentPing();
            }
        }
        
        private bool PingVersion()
        {
            INetworkPeer peer = this.AttachedPeer;
            return (peer != null) && (peer.Version > NBitcoin.Protocol.ProtocolVersion.BIP0031_VERSION);
        }

        protected override Task OnStateChangedAsync(INetworkPeer peer, NetworkPeerState oldState)
        {
            if (peer.State == NetworkPeerState.HandShaked)
                this.Ping(null);

            return Task.CompletedTask;
        }

        private void Ping(object unused)
        {
            if (Monitor.TryEnter(this.cs))
            {
                try
                {
                    INetworkPeer peer = this.AttachedPeer;

                    if (peer == null) return;
                    if (!this.PingVersion()) return;
                    if (peer.State != NetworkPeerState.HandShaked) return;
                    if (this.currentPing != null) return;

                    this.currentPing = new PingPayload();
                    this.dateSent = DateTimeOffset.UtcNow;
                    peer.SendMessageAsync(this.currentPing).GetAwaiter().GetResult();
                    this.pingTimeoutTimer = new Timer(PingTimeout, this.currentPing, (int)this.TimeoutInterval.TotalMilliseconds, Timeout.Infinite);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    Monitor.Exit(this.cs);
                }
            }
        }

        /// <summary>
        /// Send a ping asynchronously.
        /// </summary>
        public void Probe()
        {
            this.Ping(null);
        }

        private void PingTimeout(object ping)
        {
            INetworkPeer peer = this.AttachedPeer;
            if ((peer != null) && ((PingPayload)ping == this.currentPing))
                peer.Disconnect("Pong timeout for " + ((PingPayload)ping).Nonce);
        }

        private Timer pingTimeoutTimer;
        private volatile PingPayload currentPing;
        private DateTimeOffset dateSent;

        public TimeSpan Latency { get; private set; }

        private void ClearCurrentPing()
        {
            lock (this.cs)
            {
                this.currentPing = null;
                this.dateSent = default(DateTimeOffset);
                Timer timeout = this.pingTimeoutTimer;
                if (timeout != null)
                {
                    timeout.Dispose();
                    this.pingTimeoutTimer = null;
                }
            }
        }

        protected override void DetachCore()
        {
            this.ClearCurrentPing();
        }

        #region ICloneable Members

        public override object Clone()
        {
            return new PingPongBehavior()
            {
                Mode = this.Mode,
                PingInterval = this.PingInterval,
                TimeoutInterval = this.TimeoutInterval
            };
        }

        #endregion
    }
}