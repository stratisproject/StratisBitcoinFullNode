using System;
using System.Threading;

namespace NBitcoin.Protocol.Behaviors
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
    public class PingPongBehavior : NodeBehavior
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
            if ((this.AttachedNode.PeerVersion != null) && !PingVersion()) //If not handshaked, still attach (the callback will also check version).
                return;
            this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
            this.RegisterDisposable(new Timer(Ping, null, 0, (int)this.PingInterval.TotalMilliseconds));
        }

        private bool PingVersion()
        {
            Node node = this.AttachedNode;
            return (node != null) && (node.Version > NBitcoin.Protocol.ProtocolVersion.BIP0031_VERSION);
        }

        void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            if (node.State == NodeState.HandShaked)
                this.Ping(null);
        }

        void Ping(object unused)
        {
            if (Monitor.TryEnter(this.cs))
            {
                try
                {
                    Node node = this.AttachedNode;

                    if (node == null) return;
                    if (!PingVersion()) return;
                    if (node.State != NodeState.HandShaked) return;
                    if (this.currentPing != null) return;

                    this.currentPing = new PingPayload();
                    this.dateSent = DateTimeOffset.UtcNow;
                    node.SendMessageAsync(this.currentPing);
                    this.pingTimeoutTimer = new Timer(PingTimeout, this.currentPing, (int)this.TimeoutInterval.TotalMilliseconds, Timeout.Infinite);
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

        void PingTimeout(object ping)
        {
            Node node = this.AttachedNode;
            if ((node != null) && ((PingPayload)ping == this.currentPing))
                node.DisconnectAsync("Pong timeout for " + ((PingPayload)ping).Nonce);
        }

        private Timer pingTimeoutTimer;
        private volatile PingPayload currentPing;
        private DateTimeOffset dateSent;

        public TimeSpan Latency { get; private set; }

        void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            if (!this.PingVersion())
                return;

            if ((message.Message.Payload is PingPayload ping) && this.Mode.HasFlag(PingPongMode.RespondPong))
            {
                node.SendMessageAsync(new PongPayload()
                {
                    Nonce = ping.Nonce
                });
            }

            if ((message.Message.Payload is PongPayload pong)
                && this.Mode.HasFlag(PingPongMode.SendPing)
                && (this.currentPing != null)
                && (this.currentPing.Nonce == pong.Nonce))
            {
                this.Latency = DateTimeOffset.UtcNow - this.dateSent;
                this.ClearCurrentPing();
            }
        }

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
            this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
            this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
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