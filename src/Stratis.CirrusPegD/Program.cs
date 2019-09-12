using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
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
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Collateral;
using Stratis.Features.Collateral.CounterChain;
using Stratis.Features.FederatedPeg;
using Stratis.Sidechains.Networks;

namespace Stratis.CirrusPegD
{
    class Program
    {
        private const string MainchainArgument = "-mainchain";
        private const string SidechainArgument = "-sidechain";

        private static readonly Dictionary<NetworkType, Func<Network>> SidechainNetworks = new Dictionary<NetworkType, Func<Network>>
        {
            { NetworkType.Mainnet, CirrusNetwork.NetworksSelector.Mainnet },
            { NetworkType.Testnet, CirrusNetwork.NetworksSelector.Testnet },
            { NetworkType.Regtest, CirrusNetwork.NetworksSelector.Regtest }
        };

        private static readonly Dictionary<NetworkType, Func<Network>> MainChainNetworks = new Dictionary<NetworkType, Func<Network>>
        {
            { NetworkType.Mainnet, Networks.Stratis.Mainnet },
            { NetworkType.Testnet, Networks.Stratis.Testnet },
            { NetworkType.Regtest, Networks.Stratis.Regtest }
        };

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
            // TODO: Hardcode -addressindex

            var nodeSettings = new NodeSettings(networksSelector: Networks.Stratis, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, args: args)
            {
                MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
            };

            NetworkType networkType = nodeSettings.Network.NetworkType;

            var fedPegOptions = new FederatedPegOptions(
                walletSyncFromHeight: new int[] { FederatedPegSettings.StratisMainDepositStartBlock, 1, 1 }[(int)networkType]
            );

            IFullNode node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .SetCounterChainNetwork(SidechainNetworks[nodeSettings.Network.NetworkType]())
                .AddFederatedPeg(fedPegOptions)
                .UseTransactionNotification()
                .UseBlockNotification()
                .UseApi()
                .UseMempool()
                .AddRPC()
                .UsePosConsensus()
                .UseWallet()
                .AddPowPosMining()
                .Build();

            return node;
        }

        private static IFullNode GetSidechainFullNode(string[] args)
        {
            var nodeSettings = new NodeSettings(networksSelector: CirrusNetwork.NetworksSelector, protocolVersion: ProtocolVersion.CIRRUS_VERSION, args: args)
            {
                MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
            };

            NetworkType networkType = nodeSettings.Network.NetworkType;

            var fedPegOptions = new FederatedPegOptions(
                walletSyncFromHeight: new int[] { 1, 1, 1 }[(int)networkType]
            );

            IFullNode node = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .SetCounterChainNetwork(MainChainNetworks[nodeSettings.Network.NetworkType]())
                .UseFederatedPegPoAMining()
                .AddFederatedPeg(fedPegOptions)
                .CheckForPoAMembersCollateral()
                .UseTransactionNotification()
                .UseBlockNotification()
                .UseApi()
                .UseMempool()
                .AddRPC()
                .AddSmartContracts(options =>
                {
                    options.UseReflectionExecutor();
                    options.UsePoAWhitelistedContracts();
                })
                .UseSmartContractWallet()
                .Build();

            return node;
        }
    }
}
