using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
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
        [Fact(Skip = "Takes a long time to run with SBFN making blocks. Need to investigate why.")]
        public void SBFNMinesBlocks_XSyncs()
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

                // TODO: Need to troubleshoot why TestHelper.Connect() does not work here, possibly unsupported RPC method (it seems that addnode does not work for X).
                stratisNodeRpc.AddNode(stratisXNode.Endpoint, false);

                // TODO: Similarly, the 'generate' RPC call is problematic on X. Possibly returning an unexpected JSON format.
                TestHelper.MineBlocks(stratisNode, 10);

                // As we are not actually sending transactions, it does not matter that the datetime provider is substituted
                // for this test. The blocks get accepted by X despite getting generated very rapidly.
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;

                TestHelper.WaitLoop(() => stratisNodeRpc.GetBlockCount() >= 10, cancellationToken: cancellationToken);
                TestHelper.WaitLoop(() => stratisNodeRpc.GetBestBlockHash() == stratisXRpc.GetBestBlockHash(), cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Tests whether a quantity of blocks mined on X are
        /// correctly synced to an SBFN node.
        /// </summary>
        [Fact]
        public void XMinesBlocks_SBFNSyncs()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // TODO: Add the necessary executables for Linux & OSX
                return;
            }

            using (NodeBuilder builder = NodeBuilder.Create(this).WithLogsEnabled())
            {
                var network = new StratisRegTest();

                CoreNode stratisXNode = builder.CreateStratisXNode(version: "2.0.0.5").Start();

                var callback = new Action<IFullNodeBuilder>(build => build
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC());

                var config = new NodeConfigParameters();
                config.Add("whitelist", stratisXNode.Endpoint.ToString());
                config.Add("gateway", "1");

                CoreNode stratisNode = builder
                    .CreateCustomNode(callback, network, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, minProtocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: config)
                    .WithWallet().Start();

                RPCClient stratisXRpc = stratisXNode.CreateRPCClient();
                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();

                stratisNodeRpc.AddNode(stratisXNode.Endpoint, false);

                stratisXRpc.SendCommand(RPCOperations.generate, 10);

                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;

                TestHelper.WaitLoop(() => stratisXRpc.GetBlockCount() >= 10, cancellationToken: cancellationToken);
                TestHelper.WaitLoop(() => stratisXRpc.GetBestBlockHash() == stratisNodeRpc.GetBestBlockHash(), cancellationToken: cancellationToken);
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
        public void SBFNMinesTransaction_XSyncs()
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
                    .UseTestChainedHeaderTree()
                    .MockIBD());

                CoreNode stratisNode = builder.CreateCustomNode(callback, network, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION).WithWallet().Start();

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

                // Sanity check - mempools should both become empty.
                TestHelper.WaitLoop(() => stratisNodeRpc.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => stratisXRpc.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
            }
        }

        /// <summary>
        /// This test is necessary because X regards nulldata transactions as non-standard if they have a value of zero assigned.
        /// </summary>
        [Fact]
        public void SBFNCreatesOpReturnTransaction_XSyncs()
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

                // We do not want the datetime provider to be substituted,
                // so a custom builder callback has to be used.
                var callback = new Action<IFullNodeBuilder>(build => build
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC()
                    .UseTestChainedHeaderTree()
                    .MockIBD());

                CoreNode stratisNode = builder.CreateCustomNode(callback, network, protocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, minProtocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION).WithWallet().Start();

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
                //stratisNodeRpc.WalletPassphrase("password", 60);

                var transactionBuildContext = new TransactionBuildContext(stratisNode.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference("mywallet", "account 0"),
                    MinConfirmations = 1,
                    OpReturnData = "test",
                    OpReturnAmount = Money.Coins(0.01m),
                    WalletPassword = "password",
                    Recipients = new List<Recipient>() { new Recipient() { Amount = Money.Coins(1), ScriptPubKey = aliceAddress.ScriptPubKey } }
                };

                var transaction = stratisNode.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);

                stratisNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));

                TestHelper.WaitLoop(() => stratisNodeRpc.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);

                // Transaction should percolate through to X's mempool.
                TestHelper.WaitLoop(() => stratisXRpc.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);
            }
        }

        [Fact]
        public void XMinesTransaction_SBFNSyncs()
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

                var callback = new Action<IFullNodeBuilder>(build => build
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC());

                var config = new NodeConfigParameters();
                config.Add("whitelist", stratisXNode.Endpoint.ToString());
                config.Add("gateway", "1");

                CoreNode stratisNode = builder
                    .CreateCustomNode(callback, network, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, minProtocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: config)
                    .WithWallet().Start();

                RPCClient stratisXRpc = stratisXNode.CreateRPCClient();
                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();

                stratisXRpc.AddNode(stratisNode.Endpoint, false);
                stratisNodeRpc.AddNode(stratisXNode.Endpoint, false);

                stratisXRpc.SendCommand(RPCOperations.generate, 11);

                var shortCancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;

                // Without this there seems to be a race condition between the blocks all getting generated and SBFN syncing high enough to fall through the getbestblockhash check.
                TestHelper.WaitLoop(() => stratisXRpc.GetBlockCount() >= 11, cancellationToken: shortCancellationToken);

                TestHelper.WaitLoop(() => stratisNodeRpc.GetBestBlockHash() == stratisXRpc.GetBestBlockHash(), cancellationToken: shortCancellationToken);

                // Send transaction to arbitrary address from X side.
                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();
                stratisXRpc.SendCommand(RPCOperations.sendtoaddress, aliceAddress.ToString(), 1);

                TestHelper.WaitLoop(() => stratisXRpc.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);

                // Transaction should percolate through to SBFN's mempool.
                TestHelper.WaitLoop(() => stratisNodeRpc.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);

                // Now X must mine the block.
                stratisXRpc.SendCommand(RPCOperations.generate, 1);
                TestHelper.WaitLoop(() => stratisXRpc.GetBlockCount() >= 12, cancellationToken: shortCancellationToken);

                // We expect that SBFN will sync correctly.
                TestHelper.WaitLoop(() => stratisNodeRpc.GetBestBlockHash() == stratisXRpc.GetBestBlockHash(), cancellationToken: shortCancellationToken);

                // Sanity check - mempools should both become empty.
                TestHelper.WaitLoop(() => stratisNodeRpc.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => stratisXRpc.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
            }
        }

        /// <summary>
        /// Construct a small network of 3 nodes, X1 - S2 -X3.
        /// X1 generates sufficient blocks to get spendable coins.
        /// X1 creates a transaction sending a coin to an arbitrary address.
        /// S2 and X3 should get the transaction in their mempools.
        /// </summary>
        [Fact]
        [Trait("Unstable", "True")]
        public void Transaction_CreatedByXNode_TraversesSBFN_ReachesSecondXNode()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // TODO: Add the necessary executables for Linux & OSX
                return;
            }

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                CoreNode xNode1 = builder.CreateStratisXNode(version: "2.0.0.5").Start();

                var callback = new Action<IFullNodeBuilder>(build => build
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC());

                var config = new NodeConfigParameters();
                config.Add("whitelist", xNode1.Endpoint.ToString());
                config.Add("gateway", "1");

                CoreNode sbfnNode2 = builder
                    .CreateCustomNode(callback, network, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, minProtocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: config)
                    .WithWallet().Start();

                CoreNode xNode3 = builder.CreateStratisXNode(version: "2.0.0.5").Start();

                RPCClient xRpc1 = xNode1.CreateRPCClient();
                RPCClient sbfnRpc2 = sbfnNode2.CreateRPCClient();
                RPCClient xRpc3 = xNode3.CreateRPCClient();

                // Connect the nodes linearly. X does not appear to respond properly to the addnode RPC so SBFN needs to initiate.
                sbfnRpc2.AddNode(xNode1.Endpoint, false);
                sbfnRpc2.AddNode(xNode3.Endpoint, false);

                xRpc1.SendCommand(RPCOperations.generate, 11);

                var shortCancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;

                TestHelper.WaitLoop(() => xRpc1.GetBlockCount() >= 11, cancellationToken: shortCancellationToken);

                TestHelper.WaitLoop(() => xRpc1.GetBestBlockHash() == sbfnRpc2.GetBestBlockHash(), cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => xRpc1.GetBestBlockHash() == xRpc3.GetBestBlockHash(), cancellationToken: shortCancellationToken);

                // Send transaction to arbitrary address.
                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();
                xRpc1.SendCommand(RPCOperations.sendtoaddress, aliceAddress.ToString(), 1);

                TestHelper.WaitLoop(() => xRpc1.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => sbfnRpc2.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => xRpc3.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);
            }
        }

        /// <summary>
        /// Construct a small network of 3 nodes, X1 - S2 -X3.
        /// X1 generates sufficient blocks to get spendable coins.
        /// X1 creates a transaction sending a coin to an arbitrary address.
        /// S2 and X3 should get the transaction in their mempools.
        /// Now X1 mines a block.
        /// S2 and X3 should sync to the new block height.
        /// All mempools should be empty at the end.
        /// </summary>
        [Fact]
        [Trait("Unstable", "True")]
        public void Transaction_TraversesNodes_AndIsMined_AndNodesSync()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // TODO: Add the necessary executables for Linux & OSX
                return;
            }

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                CoreNode xNode1 = builder.CreateStratisXNode(version: "2.0.0.5").Start();

                var callback = new Action<IFullNodeBuilder>(build => build
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC());

                var config = new NodeConfigParameters();
                config.Add("whitelist", xNode1.Endpoint.ToString());
                config.Add("gateway", "1");

                CoreNode sbfnNode2 = builder
                    .CreateCustomNode(callback, network, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, minProtocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: config)
                    .WithWallet().Start();

                CoreNode xNode3 = builder.CreateStratisXNode(version: "2.0.0.5").Start();

                RPCClient xRpc1 = xNode1.CreateRPCClient();
                RPCClient sbfnRpc2 = sbfnNode2.CreateRPCClient();
                RPCClient xRpc3 = xNode3.CreateRPCClient();

                sbfnRpc2.AddNode(xNode1.Endpoint, false);
                sbfnRpc2.AddNode(xNode3.Endpoint, false);

                xRpc1.SendCommand(RPCOperations.generate, 11);

                var shortCancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;

                TestHelper.WaitLoop(() => xRpc1.GetBlockCount() >= 11, cancellationToken: shortCancellationToken);

                TestHelper.WaitLoop(() => xRpc1.GetBestBlockHash() == sbfnRpc2.GetBestBlockHash(), cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => xRpc1.GetBestBlockHash() == xRpc3.GetBestBlockHash(), cancellationToken: shortCancellationToken);

                // Send transaction to arbitrary address.
                var alice = new Key().GetBitcoinSecret(network);
                var aliceAddress = alice.GetAddress();
                xRpc1.SendCommand(RPCOperations.sendtoaddress, aliceAddress.ToString(), 1);

                TestHelper.WaitLoop(() => xRpc1.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => sbfnRpc2.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => xRpc3.GetRawMempool().Length == 1, cancellationToken: shortCancellationToken);

                // TODO: Until #2468 is fixed we need an X node to mine the block so it doesn't get rejected.
                xRpc1.SendCommand(RPCOperations.generate, 1);
                TestHelper.WaitLoop(() => xRpc1.GetBlockCount() >= 12, cancellationToken: shortCancellationToken);

                // We expect that SBFN and the other X node will sync correctly.
                TestHelper.WaitLoop(() => sbfnRpc2.GetBestBlockHash() == xRpc1.GetBestBlockHash(), cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => xRpc3.GetBestBlockHash() == xRpc1.GetBestBlockHash(), cancellationToken: shortCancellationToken);

                // Sanity check - mempools should all become empty.
                TestHelper.WaitLoop(() => xRpc1.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => sbfnRpc2.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
                TestHelper.WaitLoop(() => xRpc3.GetRawMempool().Length == 0, cancellationToken: shortCancellationToken);
            }
        }
    }
}