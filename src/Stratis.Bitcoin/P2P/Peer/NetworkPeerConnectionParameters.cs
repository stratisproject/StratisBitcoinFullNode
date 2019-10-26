using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NetworkPeerConnectionParameters
    {
        /// <summary>Send addr unsollicited message of the AddressFrom peer when passing to Handshaked state.</summary>
        public bool Advertize { get; set; }

        public ProtocolVersion Version { get; set; }

        /// <summary>If true, the node will receive all incoming transactions if no bloomfilter are set.</summary>
        public bool IsRelay { get; set; }

        public NetworkPeerServices Services { get; set; }

        public TransactionOptions PreferredTransactionOptions { get; set; }

        public string UserAgent { get; set; }

        public int ReceiveBufferSize { get; set; }

        public int SendBufferSize { get; set; }

        public IPEndPoint AddressFrom { get; set; }

        public ulong? Nonce { get; set; }

        public CancellationToken ConnectCancellation { get; set; }

        public List<INetworkPeerBehavior> TemplateBehaviors { get; }

        public NetworkPeerConnectionParameters()
        {
            this.Version = ProtocolVersion.PROTOCOL_VERSION;
            this.IsRelay = true;
            this.Services = NetworkPeerServices.Nothing;
            this.ConnectCancellation = default(CancellationToken);
            this.TemplateBehaviors = new List<INetworkPeerBehavior>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Use max supported by MAC OSX Yosemite/Mavericks/Sierra (https://fasterdata.es.net/host-tuning/osx/)
                this.ReceiveBufferSize = 1048576;
                this.SendBufferSize = 1048576;
            }
            else
            {
                this.ReceiveBufferSize = 1000 * 5000;
                this.SendBufferSize = 1000 * 5000;
            }

            this.UserAgent = VersionPayload.GetNBitcoinUserAgent();
            this.PreferredTransactionOptions = TransactionOptions.All;
            this.Nonce = RandomUtils.GetUInt64();
        }

        public NetworkPeerConnectionParameters SetFrom(NetworkPeerConnectionParameters other)
        {
            this.Version = other.Version;
            this.IsRelay = other.IsRelay;
            this.Services = other.Services;
            this.ReceiveBufferSize = other.ReceiveBufferSize;
            this.SendBufferSize = other.SendBufferSize;
            this.ConnectCancellation = other.ConnectCancellation;
            this.UserAgent = other.UserAgent;
            this.AddressFrom = other.AddressFrom;
            this.Nonce = other.Nonce;
            this.Advertize = other.Advertize;
            this.PreferredTransactionOptions = other.PreferredTransactionOptions;

            foreach (INetworkPeerBehavior behavior in other.TemplateBehaviors)
            {
                this.TemplateBehaviors.Add(behavior.Clone());
            }

            return this;
        }

        [NoTrace]
        public NetworkPeerConnectionParameters Clone()
        {
            return new NetworkPeerConnectionParameters().SetFrom(this);
        }

        public VersionPayload CreateVersion(IPEndPoint externalAddressEndPoint, IPEndPoint peerAddress, Network network, DateTimeOffset timeStamp)
        {
            var version = new VersionPayload()
            {
                Nonce = this.Nonce == null ? RandomUtils.GetUInt64() : this.Nonce.Value,
                UserAgent = this.UserAgent,
                Version = this.Version,
                Timestamp = timeStamp,
                AddressReceiver = peerAddress,
                AddressFrom = externalAddressEndPoint,
                Relay = this.IsRelay,
                Services = this.Services
            };

            return version;
        }
    }
}