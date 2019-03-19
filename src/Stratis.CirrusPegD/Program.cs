﻿using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg;
using Stratis.Sidechains.Networks;

namespace Stratis.CirrusPegD
{
    class Program
    {
        private const string MainchainArgument = "-mainchain";
        private const string SidechainArgument = "-sidechain";

        private static void Main(string[] args)
        {
            RunFederationGatewayAsync(args).Wait();
        }

        private static async Task RunFederationGatewayAsync(string[] args)
        {
            try
            {
                bool isMainchainNode = args.FirstOrDefault(a => a.ToLower() == MainchainArgument) != null;
                bool isSidechainNode = args.FirstOrDefault(a => a.ToLower() == SidechainArgument) != null;

                if (isSidechainNode == isMainchainNode)
                {
                    throw new ArgumentException($"Gateway node needs to be started specifying either a {SidechainArgument} or a {MainchainArgument} argument");
                }

                IFullNode node = isMainchainNode ? GetMainchainFullNode(args) : GetSidechainFullNode(args);

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        private static IFullNode GetMainchainFullNode(string[] args)
        {
            var nodeSettings = new NodeSettings(networksSelector: Networks.Stratis, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, args: args)
            {
                MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
            };

            IFullNode node = new FullNodeBuilder()
                .AddCommonFeatures(nodeSettings)
                .UsePosConsensus()
                .UseWallet()
                .AddPowPosMining()
                .Build();

            return node;
        }

        private static IFullNode GetSidechainFullNode(string[] args)
        {
            var nodeSettings = new NodeSettings(networksSelector: FederatedPegNetwork.NetworksSelector, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, args: args)
            {
                MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
            };

            IFullNode node = new FullNodeBuilder()
                .AddCommonFeatures(nodeSettings)
                .AddSmartContracts(options =>
                {
                    options.UseReflectionExecutor();
                })
                .UseSmartContractWallet()
                .UseFederatedPegPoAMining()
                .Build();

            return node;
        }
    }

    internal static class CommonFeaturesExtension
    {
        internal static IFullNodeBuilder AddCommonFeatures(this IFullNodeBuilder fullNodeBuilder, NodeSettings nodeSettings)
        {
            return fullNodeBuilder
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .AddFederationGateway()
                .UseTransactionNotification()
                .UseBlockNotification()
                .UseApi()
                .UseMempool()
                .AddRPC();
        }
    }
}
