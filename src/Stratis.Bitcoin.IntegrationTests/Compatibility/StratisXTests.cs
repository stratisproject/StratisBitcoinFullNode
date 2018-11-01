using System;
using System.Runtime.InteropServices;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Compatibility
{
    public class StratisXTests
    {
        /// <summary>
        /// Tests whether a quantity of blocks mined on SBFN are
        /// correctly synced to a stratisX node.
        /// </summary>
        [Fact]
        public void SBFNMinesBlocksXSyncs()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // TODO: Add the necessary executables for Linux & OSX
                return;
            }

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                CoreNode stratisXNode = builder.CreateStratisXNode(version: "2.0.0.5").Start();
                CoreNode stratisNode = builder.CreateStratisPosNode(network).WithWallet().Start();

                RPCClient stratisXRpc = stratisXNode.CreateRPCClient();
                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();

                // TODO: Need to troubleshoot why TestHelper.Connect() does not work here, possibly unsupported RPC method.
                stratisXRpc.AddNode(stratisNode.Endpoint, false);
                stratisNodeRpc.AddNode(stratisXNode.Endpoint, false);

                // TODO: Similarly, the 'generate' RPC call is problematic on X. Possibly returning an unexpected JSON format.
                TestHelper.MineBlocks(stratisNode, 10);

                // As we are not actually sending transactions, it does not
                // matter that the datetime provider is substituted for this
                // test. The blocks get accepted by X despite getting generated
                // very rapidly.
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == stratisXNode.CreateRPCClient().GetBestBlockHash(), cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Tests whether a transaction relayed by SBFN appears in the
        /// stratisX mempool. The transaction is then mined into a
        /// block by SBFN, and the block must be accepted by stratisX.
        /// </summary>
        /// <remarks>Takes a while to run as block generation cannot
        /// be sped up for this test.</remarks>
        [Fact(Skip = "Awaiting fix for issue #2468")]
        public void SBFNMinesTransactionAndXSyncs()
        {
            // TODO: Currently fails due to issue #2468 (coinbase
            // reward on stratisX cannot be >4. No fees are allowed)

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // TODO: Add the necessary executables for Linux & OSX
                return;
            }

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                CoreNode stratisXNode = builder.CreateStratisXNode(version: "2.0.0.5").Start();

                // We do not want the datetime provider to be substituted,
                // so a custom builder callback has to be used.
                var callback = new Action<IFullNodeBuilder>(build => build
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC()
                    .MockIBD());

                CoreNode stratisNode = builder.CreateCustomNode(callback, network).WithWallet().Start();

                RPCClient stratisXRpc = stratisXNode.CreateRPCClient();
                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();

                stratisXRpc.AddNode(stratisNode.Endpoint, false);
                stratisNodeRpc.AddNode(stratisXNode.Endpoint, false);

                TestHelper.MineBlocks(stratisNode, 11);

                // It takes a reasonable amount of time for blocks to be generated without
                // the datetime provider substitution.
                var longCancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(15)).Token;
                var shortCancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;

                TestHelper.WaitLoop(() => stratisNodeRpc.GetBestBlockHash() == stratisXRpc.GetBestBlockHash(), cancellationToken: longCancellationToken);

                // Send transaction to arbitrary address from SBFN side.
                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();
                stratisNodeRpc.WalletPassphrase("password", 60);
                stratisNodeRpc.SendToAddress(aliceAddress, Money.Coins(1.0m));

                TestHelper.WaitLoop(() => stratisNodeRpc.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);

                // Transaction should percolate through to X's mempool.
                TestHelper.WaitLoop(() => stratisXRpc.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);

                // Now SBFN must mine the block.
                TestHelper.MineBlocks(stratisNode, 1);

                // We expect that X will sync correctly.
                TestHelper.WaitLoop(() => stratisNodeRpc.GetBestBlockHash() == stratisXRpc.GetBestBlockHash(), cancellationToken: shortCancellationToken);

                // Sanity check - mempools should both be empty.
                TestHelper.WaitLoop(() => stratisNodeRpc.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => stratisXRpc.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
            }
        }
    }
}