using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.FederatedSidechains.IntegrationTests.Common
{
    public static class NodeBuilderExtensions
    {
        public static CoreNode CreateCustomNodeWithFreeApiPort(this NodeBuilder nodeBuilder, bool start, Action<IFullNodeBuilder> callback, Network network, ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION, string configFileName = "custom.conf", string agent = "Custom")
        {
            var currentPort = nodeBuilder.ConfigParameters.TryGetValue("apiport", out string currentPortAsString)
                ? uint.Parse(currentPortAsString)
                : (uint?)null;
            nodeBuilder.ConfigParameters["apiport"] = FindFreePort(++currentPort).ToString();
            var node = nodeBuilder.CreateCustomNode(start, callback, network, protocolVersion, configFileName, agent);
            return node;
        }

        public static CoreNode CreatePowPosMiningNode(this NodeBuilder nodeBuilder,
            Network network, bool start = false, string agent = "PowPosMining")
        {
            var node = nodeBuilder.CreateCustomNodeWithFreeApiPort(start, fullNodeBuilder =>
            {
                fullNodeBuilder
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .UseApi()
                    .AddRPC()
                    .MockIBD();
            }, network, agent: agent);

            return node;
        }

        public static CoreNode CreatePowPosSidechainApiMiningNode(this NodeBuilder nodeBuilder,
            Network network, bool start = false, string agent = "PowPosMining")
        {
            var node = nodeBuilder.CreateCustomNodeWithFreeApiPort(start, fullNodeBuilder =>
            {
                fullNodeBuilder
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .UseApi()
                    .UseSidechains()
                    .AddRPC()
                    .MockIBD();
            }, network, agent: agent);

            return node;
        }

        private static int FindFreePort(uint? currentPort = null)
        {
            var port = currentPort ?? (uint)Guid.NewGuid().GetHashCode() % 4000;
            while (true)
            {
                try
                {
                    var tcpListener = new TcpListener(IPAddress.Loopback, (int)port);
                    tcpListener.Start();
                    tcpListener.Stop();
                    return (int)port;
                }
                catch (SocketException)
                {
                    port = (uint)Guid.NewGuid().GetHashCode() % 4000; ;
                }
            }
        }
    }
}