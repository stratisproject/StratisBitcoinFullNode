using System;
using System.Net;
using System.Threading;
using NBitcoin.Protocol.Behaviors;

namespace NBitcoin.Protocol
{
    public class NodeConnectionParameters
    {
        /// <summary>Send addr unsollicited message of the AddressFrom peer when passing to Handshaked state.</summary>
        public bool Advertize { get; set; }
        public ProtocolVersion Version { get; set; }

        /// <summary>If true, the node will receive all incoming transactions if no bloomfilter are set.</summary>
        public bool IsRelay { get; set; }

        public NodeServices Services { get; set; }

        public TransactionOptions PreferredTransactionOptions { get; set; }

        public string UserAgent { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }

        /// <summary>Whether we reuse a 1MB buffer for deserializing messages, for limiting GC activity (Default: <c>true</c>).</summary>
        public bool ReuseBuffer { get; set; }
        public CancellationToken ConnectCancellation { get; set; }

        public NodeBehaviorsCollection TemplateBehaviors { get; private set; }

        public IPEndPoint AddressFrom { get; set; }

        public ulong? Nonce { get; set; }

        public NodeConnectionParameters()
        {
            this.ReuseBuffer = true;
            this.TemplateBehaviors = new NodeBehaviorsCollection(null);
            this.TemplateBehaviors.Add(new PingPongBehavior());
            this.Version = ProtocolVersion.PROTOCOL_VERSION;
            this.IsRelay = true;
            this.Services = NodeServices.Nothing;
            this.ConnectCancellation = default(CancellationToken);
            this.ReceiveBufferSize = 1000 * 5000;
            this.SendBufferSize = 1000 * 1000;
            this.UserAgent = VersionPayload.GetNBitcoinUserAgent();
            this.PreferredTransactionOptions = TransactionOptions.All;
        }

        public NodeConnectionParameters Clone()
        {
            var clone = new NodeConnectionParameters()
            {
                Version = this.Version,
                IsRelay = this.IsRelay,
                Services = this.Services,
                ReceiveBufferSize = this.ReceiveBufferSize,
                SendBufferSize = this.SendBufferSize,
                ConnectCancellation = this.ConnectCancellation,
                UserAgent = this.UserAgent,
                AddressFrom = this.AddressFrom,
                Nonce = this.Nonce,
                Advertize = this.Advertize,
                ReuseBuffer = this.ReuseBuffer,
                PreferredTransactionOptions = this.PreferredTransactionOptions
            };

            foreach (INodeBehavior behavior in this.TemplateBehaviors)
                this.TemplateBehaviors.Add(behavior.Clone());

            return clone;
        }

        public VersionPayload CreateVersion(IPEndPoint peer, Network network)
        {
            VersionPayload version = new VersionPayload()
            {
                Nonce = this.Nonce == null ? RandomUtils.GetUInt64() : this.Nonce.Value,
                UserAgent = this.UserAgent,
                Version = this.Version,
                Timestamp = DateTimeOffset.UtcNow,
                AddressReceiver = peer,
                AddressFrom = this.AddressFrom ?? new IPEndPoint(IPAddress.Parse("0.0.0.0").MapToIPv6Ex(), network.DefaultPort),
                Relay = this.IsRelay,
                Services = this.Services
            };
            return version;
        }
    }
}