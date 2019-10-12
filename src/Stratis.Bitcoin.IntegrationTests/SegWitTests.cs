using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    // TODO: This is also used in the block store integration tests, perhaps move it into the common namespace
    /// <summary>
    /// Used for recording messages coming into a test node. Does not respond to them in any way.
    /// </summary>
    internal class TestBehavior : NetworkPeerBehavior
    {
        public readonly Dictionary<string, List<IncomingMessage>> receivedMessageTracker = new Dictionary<string, List<IncomingMessage>>();

        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            try
            {
                await this.ProcessMessageAsync(peer, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ProcessMessageAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!this.receivedMessageTracker.ContainsKey(message.Message.Payload.Command))
                this.receivedMessageTracker[message.Message.Payload.Command] = new List<IncomingMessage>();

            this.receivedMessageTracker[message.Message.Payload.Command].Add(message);
        }

        public override object Clone()
        {
            var res = new TestBehavior();

            return res;
        }
    }

    public class SegWitTests
    {
        [Fact]
        public void TestSegwit_MinedOnCore_ActivatedOn_StratisNode()
        {
            // This test only verifies that the BIP9 machinery is operating correctly on the Stratis PoW node.
            // Since newer versions of Bitcoin Core have segwit always activated from genesis, there is no need to
            // perform the reverse version of this test. Much more important are the P2P and mempool tests for
            // segwit transactions.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode coreNode = builder.CreateBitcoinCoreNode(version: "0.15.1");
                coreNode.Start();

                CoreNode stratisNode = builder.CreateStratisPowNode(KnownNetworks.RegTest).Start();

                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();
                RPCClient coreRpc = coreNode.CreateRPCClient();

                coreRpc.AddNode(stratisNode.Endpoint, false);
                stratisNodeRpc.AddNode(coreNode.Endpoint, false);

                // Core (in version 0.15.1) only mines segwit blocks above a certain height on regtest
                // See issue for more details https://github.com/stratisproject/StratisBitcoinFullNode/issues/1028
                BIP9DeploymentsParameters prevSegwitDeployment = KnownNetworks.RegTest.Consensus.BIP9Deployments[BitcoinBIP9Deployments.Segwit];
                KnownNetworks.RegTest.Consensus.BIP9Deployments[BitcoinBIP9Deployments.Segwit] = new BIP9DeploymentsParameters("Test", 1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

                try
                {
                    // Generate 450 blocks, block 431 will be segwit activated.
                    coreRpc.Generate(450);
                    var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token;
                    TestBase.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash(), cancellationToken: cancellationToken);

                    // Segwit activation on Bitcoin regtest.
                    // - On regtest deployment state changes every 144 blocks, the threshold for activating a rule is 108 blocks.
                    // Segwit deployment status should be:
                    // - Defined up to block 142.
                    // - Started at block 143 to block 286.
                    // - LockedIn 287 (as segwit should already be signaled in blocks).
                    // - Active at block 431.

                    var consensusLoop = stratisNode.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                    ThresholdState[] segwitDefinedState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(142));
                    ThresholdState[] segwitStartedState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(143));
                    ThresholdState[] segwitLockedInState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(287));
                    ThresholdState[] segwitActiveState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(431));

                    // Check that segwit got activated at block 431.
                    Assert.Equal(ThresholdState.Defined, segwitDefinedState.GetValue((int)BitcoinBIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.Started, segwitStartedState.GetValue((int)BitcoinBIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.LockedIn, segwitLockedInState.GetValue((int)BitcoinBIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.Active, segwitActiveState.GetValue((int)BitcoinBIP9Deployments.Segwit));
                }
                finally
                {
                    KnownNetworks.RegTest.Consensus.BIP9Deployments[BitcoinBIP9Deployments.Segwit] = prevSegwitDeployment;
                }
            }
        }

        [Fact]
        public void CanCheckBlockWithWitness()
        {
            var network = KnownNetworks.RegTest;

            Block block = Block.Load(Encoders.Hex.DecodeData("000000202f6f6a130549473222411b5c6f54150d63b32aadf10e57f7d563cfc7010000001e28204471ef9ef11acd73543894a96a3044932b85e99889e731322a8ec28a9f9ae9fc56ffff011d0011b40202010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff2c028027266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779010effffffff0250b5062a0100000023210263ed47e995cbbf1bc560101e3b76c6bdb1b094a185450cea533781ce598ff2b6ac0000000000000000266a24aa21a9ed09154465f26a2a4144739eba3e83b3e9ae6a1f69566eae7dc3747d48f1183779012000000000000000000000000000000000000000000000000000000000000000000000000001000000000101cecd90cd38ac6858c47f2fe9f28145d6e18f9c5abc7ef1a41e2f19e6fe0362580100000000ffffffff0130b48d06000000001976a91405481b7f1d90c5a167a15b00e8af76eb6984ea5988ac0247304402206104c335e4adbb920184957f9f710b09de17d015329fde6807b9d321fd2142db02200b24ad996b4aa4ff103000348b5ad690abfd9fddae546af9e568394ed4a83113012103a65786c1a48d4167aca08cf6eb8eed081e13f45c02dc6000fd8f3bb16242579a00000000"), network.Consensus.ConsensusFactory);

            var consensusFlags = new DeploymentFlags
            {
                ScriptFlags = ScriptVerify.Witness | ScriptVerify.P2SH | ScriptVerify.Standard,
                LockTimeFlags = Transaction.LockTimeFlags.MedianTimePast,
                EnforceBIP34 = true
            };

            var context = new RuleContext
            {
                Time = DateTimeOffset.UtcNow,
                ValidationContext = new ValidationContext { BlockToValidate = block },
                Flags = consensusFlags,
            };

            network.Consensus.Options = new ConsensusOptions();
            new WitnessCommitmentsRule().ValidateWitnessCommitment(context, network).GetAwaiter().GetResult();

            var rule = new CheckPowTransactionRule();
            var options = network.Consensus.Options;
            foreach (Transaction tx in block.Transactions)
                rule.CheckTransaction(network, options, tx);
        }

        [Fact]
        public void CanCheckBlockWithWitnessInInput()
        {
            var network = KnownNetworks.StratisRegTest;

            var blockHex = "000000202556b759011bdeb042b65a06b36c1d8f42f1e14e38c416c4f6ef5088bdc3ed1cb4cf11a1ca0990feb5d5f97539248a41e6cf888fe2d4acff52c59ece09844256907f495affff001b000000000201000000907f495a0001010000000000000000000000000000000000000000000000000000000000000000ffffffff290117006a24aa21a9ed45bcfb983571363bb71eacddafc5329fe341b966551ad4e44f6b0d92244f6301ffffffff01000000000000000000012000000000000000000000000000000000000000000000000000000000000000000000000001000000907f495a01d335db6f66303b5dc2ece709bc59c2b5c5682b3633663382291b65d188d1cd2a000000006b483045022100ac03651d705193814fedeb3c22235607a235c14b50efe126027c1388d5d1aa1702204e22fa85e7db05a6e02ae3c6cffb105ca4f4d765089e63c59b1d79699dd69aea0121034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcffffffff09000000000000000000204c988a215a04002321034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcac204c988a215a04002321034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcac204c988a215a04002321034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcac204c988a215a04002321034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcac204c988a215a04002321034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcac204c988a215a04002321034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcac204c988a215a04002321034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcac204c988a215a04002321034b8aa350c8e3879206e577f6a55ab7ffee8233b6aa156ba0f950c988593582bcac0000000046304402202b443911b7eaaa80a0acb372832fd81cb9436497fa80717195bfdb726dee2157022013ece143268a772f9a844541cb679fb3bedb3ebcd299ac110c3833b9664787be";

            Block block = Block.Load(Encoders.Hex.DecodeData(blockHex), network.Consensus.ConsensusFactory);

            var consensusFlags = new DeploymentFlags
            {
                ScriptFlags = ScriptVerify.Witness | ScriptVerify.P2SH | ScriptVerify.Standard,
                LockTimeFlags = Transaction.LockTimeFlags.MedianTimePast,
                EnforceBIP34 = true
            };

            var context = new RuleContext
            {
                Time = DateTimeOffset.UtcNow,
                ValidationContext = new ValidationContext { BlockToValidate = block },
                Flags = consensusFlags,
            };

            network.Consensus.Options = new ConsensusOptions();
            new WitnessCommitmentsRule().ValidateWitnessCommitment(context, network).GetAwaiter().GetResult();

            var rule = new CheckPowTransactionRule();
            var options = network.Consensus.Options;
            foreach (Transaction tx in block.Transactions)
                rule.CheckTransaction(network, options, tx);
        }

        [Fact]
        public void SegwitActivatedOnStratisNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                // Set the date ranges such that ColdStaking will 'Start' immediately after the initial confirmation window.
                network.Consensus.BIP9Deployments[StratisBIP9Deployments.Segwit] = new BIP9DeploymentsParameters("Test", 1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

                // Set a small confirmation window to reduce time taken by this test.
                network.Consensus.MinerConfirmationWindow = 10;

                // Minimum number of 'votes' required within the confirmation window to reach 'LockedIn' state.
                network.Consensus.RuleChangeActivationThreshold = 8;

                CoreNode stratisNode = builder.CreateStratisPosNode(network).WithWallet();
                stratisNode.Start();

                // Deployment activation:
                // - Deployment state changes every 'MinerConfirmationWindow' blocks.
                // - Remains in 'Defined' state until 'startedHeight'.
                // - Changes to 'Started' state at 'startedHeight'.
                // - Changes to 'LockedIn' state at 'lockedInHeight' (we are assuming that in this test every mined block will signal Segwit support)
                // - Changes to 'Active' state at 'activeHeight'.
                int startedHeight = network.Consensus.MinerConfirmationWindow - 1;
                int lockedInHeight = startedHeight + network.Consensus.MinerConfirmationWindow;
                int activeHeight = lockedInHeight + network.Consensus.MinerConfirmationWindow;

                // Generate enough blocks to cover all state changes.
                TestHelper.MineBlocks(stratisNode, activeHeight + 1);

                // Check that Segwit states got updated as expected.
                ThresholdConditionCache cache = (stratisNode.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine).NodeDeployments.BIP9;
                Assert.Equal(ThresholdState.Defined, cache.GetState(stratisNode.FullNode.ChainIndexer.GetHeader(startedHeight - 1), StratisBIP9Deployments.Segwit));
                Assert.Equal(ThresholdState.Started, cache.GetState(stratisNode.FullNode.ChainIndexer.GetHeader(startedHeight), StratisBIP9Deployments.Segwit));
                Assert.Equal(ThresholdState.LockedIn, cache.GetState(stratisNode.FullNode.ChainIndexer.GetHeader(lockedInHeight), StratisBIP9Deployments.Segwit));
                Assert.Equal(ThresholdState.Active, cache.GetState(stratisNode.FullNode.ChainIndexer.GetHeader(activeHeight), StratisBIP9Deployments.Segwit));

                // Verify that the block created before activation does not have the 'Witness' script flag set.
                var rulesEngine = stratisNode.FullNode.NodeService<IConsensusRuleEngine>();
                ChainedHeader prevHeader = stratisNode.FullNode.ChainIndexer.GetHeader(activeHeight - 1);
                DeploymentFlags flags1 = (rulesEngine as ConsensusRuleEngine).NodeDeployments.GetFlags(prevHeader);
                Assert.Equal(0, (int)(flags1.ScriptFlags & ScriptVerify.Witness));

                // Verify that the block created after activation has the 'Witness' flag set.
                DeploymentFlags flags2 = (rulesEngine as ConsensusRuleEngine).NodeDeployments.GetFlags(stratisNode.FullNode.ChainIndexer.Tip);
                Assert.NotEqual(0, (int)(flags2.ScriptFlags & ScriptVerify.Witness));
            }
        }

        [Fact]
        public void TestSegwit_AlwaysActivatedOn_StratisNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode coreNode = builder.CreateBitcoinCoreNode(version: "0.18.0", useNewConfigStyle: true);
                coreNode.Start();

                CoreNode stratisNode = builder.CreateStratisPowNode(KnownNetworks.RegTest).Start();

                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();
                RPCClient coreRpc = coreNode.CreateRPCClient();

                coreRpc.AddNode(stratisNode.Endpoint, false);
                stratisNodeRpc.AddNode(coreNode.Endpoint, false);

                coreRpc.Generate(1);
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestBase.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash(), cancellationToken: cancellationToken);

                var consensusLoop = stratisNode.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                ThresholdState[] segwitActiveState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(1));

                // Check that segwit got activated at genesis.
                Assert.Equal(ThresholdState.Active, segwitActiveState.GetValue((int)BitcoinBIP9Deployments.Segwit));
            }
        }

        [Fact]
        public void MineSegwitBlock()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Even though we are mining, we still want to use PoS consensus rules.
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).Start();

                // Create a Segwit P2WPKH scriptPubKey.
                var script = new Key().PubKey.WitHash.ScriptPubKey;

                var miner = node.FullNode.NodeService<IPowMining>() as PowMining;
                List<uint256> res = miner.GenerateBlocks(new ReserveScript(script), 1, int.MaxValue);

                // Retrieve mined block.
                Block block = node.FullNode.ChainIndexer.GetHeader(res.First()).Block;

                // Confirm that the mined block is Segwit-ted.
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node.FullNode.Network, block);

                // We presume that the consensus rules are checking the actual validity of the commitment, we just ensure that it exists here.
                Assert.NotNull(commitment);
            }
        }

        [Fact]
        public void StakeSegwitBlock()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Even though we are mining, we still want to use PoS consensus rules.
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                // Need the premine to be past coinbase maturity so that we can stake with it.
                RPCClient rpc = node.CreateRPCClient();
                rpc.Generate(12);

                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() >= 12, cancellationToken: cancellationToken);

                // Now need to start staking.
                var staker = node.FullNode.NodeService<IPosMinting>() as PosMinting;

                staker.Stake(new WalletSecret()
                {
                    WalletName = node.WalletName,
                    WalletPassword = node.WalletPassword
                });

                // Wait for the chain height to increase.
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);

                // Get the last staked block.
                Block block = node.FullNode.ChainIndexer.Tip.Block;

                // Confirm that the staked block is Segwit-ted.
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node.FullNode.Network, block);

                // We presume that the consensus rules are checking the actual validity of the commitment, we just ensure that it exists here.
                Assert.NotNull(commitment);
            }
        }

        [Fact]
        public void StakeSegwitBlock_UsingOnlySegwitUTXOs()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Even though we are mining, we still want to use PoS consensus rules.
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                var address = BitcoinWitPubKeyAddress.Create(node.FullNode.WalletManager().GetUnusedAddress().Bech32Address, KnownNetworks.StratisRegTest);

                // A P2WPKH scriptPubKey - so that funds get mined into the node's wallet as segwit UTXOs
                var script = address.ScriptPubKey;

                // Need the premine to be past coinbase maturity so that we can stake with it.
                var miner = node.FullNode.NodeService<IPowMining>() as PowMining;
                List<uint256> res = miner.GenerateBlocks(new ReserveScript(script), 12, int.MaxValue);
                
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() >= 12, cancellationToken: cancellationToken);
                TestBase.WaitLoop(() => node.FullNode.WalletManager().WalletTipHeight >= 12, cancellationToken: cancellationToken);

                // Check that every UTXO in the wallet has a Segwit scriptPubKey
                var scriptPubKeys = node.FullNode.WalletManager().GetAccounts(node.WalletName).SelectMany(a => a.GetCombinedAddresses()).SelectMany(b => b.Transactions).Select(c => c.ScriptPubKey);

                foreach (Script scriptPubKey in scriptPubKeys)
                    Assert.True(scriptPubKey.IsScriptType(ScriptType.P2WPKH));

                // Now need to start staking.
                var staker = node.FullNode.NodeService<IPosMinting>() as PosMinting;

                staker.Stake(new WalletSecret()
                {
                    WalletName = node.WalletName,
                    WalletPassword = node.WalletPassword
                });

                // Wait for the chain height to increase.
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);

                // Get the last staked block.
                Block block = node.FullNode.ChainIndexer.Tip.Block;

                // Confirm that the staked block is Segwit-ted.
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node.FullNode.Network, block);

                // We presume that the consensus rules are checking the actual validity of the commitment, we just ensure that it exists here.
                Assert.NotNull(commitment);
            }
        }

        [Fact]
        public void CheckSegwitP2PSerialisationForWitnessNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).Start();
                CoreNode listener = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).Start();

                IConnectionManager listenerConnMan = listener.FullNode.NodeService<IConnectionManager>();
                listenerConnMan.Parameters.TemplateBehaviors.Add(new TestBehavior());

                // The listener node will have default settings, i.e. it should ask for witness data in P2P messages.
                Assert.True(listenerConnMan.Parameters.Services.HasFlag(NetworkPeerServices.NODE_WITNESS));

                TestHelper.Connect(listener, node);

                // Mine a Segwit block on the first node.
                var script = new Key().PubKey.WitHash.ScriptPubKey;
                var miner = node.FullNode.NodeService<IPowMining>() as PowMining;
                List<uint256> res = miner.GenerateBlocks(new ReserveScript(script), 1, int.MaxValue);
                Block block = node.FullNode.ChainIndexer.GetHeader(res.First()).Block;
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node.FullNode.Network, block);
                Assert.NotNull(commitment);

                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetBlockCount() >= 1, cancellationToken: cancellationToken);

                // We need to capture a message on the witness-enabled destination node and see that it contains a block serialised with witness data.
                INetworkPeer connectedPeer = listenerConnMan.ConnectedPeers.FindByEndpoint(node.Endpoint);
                TestBehavior testBehavior = connectedPeer.Behavior<TestBehavior>();

                var blockMessages = testBehavior.receivedMessageTracker["block"];
                var blockReceived = blockMessages.First();

                var receivedBlock = blockReceived.Message.Payload as BlockPayload;
                var parsedBlock = receivedBlock.Obj;
                var nonWitnessBlock = parsedBlock.WithOptions(listener.FullNode.Network.Consensus.ConsensusFactory, TransactionOptions.None);

                Assert.True(parsedBlock.GetSerializedSize() > nonWitnessBlock.GetSerializedSize());
            }
        }

        [Fact]
        public void CheckSegwitP2PSerialisationForNonWitnessNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // We have to name the networks differently because the NBitcoin network registration won't allow two identical networks to coexist otherwise.
                var network = new StratisRegTest();
                network.SetPrivatePropertyValue("Name", "StratisRegTestWithDeployments");
                Assert.NotNull(network.Consensus.BIP9Deployments[2]);

                var networkNoBIP9 = new StratisRegTest();
                networkNoBIP9.SetPrivatePropertyValue("Name", "StratisRegTestWithoutDeployments");
                Assert.NotNull(networkNoBIP9.Consensus.BIP9Deployments[2]);

                // Remove BIP9 deployments (i.e. segwit).
                for (int i = 0; i < networkNoBIP9.Consensus.BIP9Deployments.Length; i++)
                    networkNoBIP9.Consensus.BIP9Deployments[i] = null;

                // Ensure the workaround had the desired effect.
                Assert.Null(networkNoBIP9.Consensus.BIP9Deployments[2]);
                Assert.NotNull(network.Consensus.BIP9Deployments[2]);

                // Explicitly use new & separate instances of StratisRegTest because we modified the BIP9 deployments on one instance.
                CoreNode node = builder.CreateStratisPosNode(network).Start();
                CoreNode listener = builder.CreateStratisPosNode(networkNoBIP9).Start();

                // Sanity check.
                Assert.Null(listener.FullNode.Network.Consensus.BIP9Deployments[2]);
                Assert.NotNull(node.FullNode.Network.Consensus.BIP9Deployments[2]);

                // By disabling Segwit on the listener node we also prevent the WitnessCommitments rule from rejecting the mining node's blocks once we modify the listener's peer services.

                IConnectionManager listenerConnMan = listener.FullNode.NodeService<IConnectionManager>();
                listenerConnMan.Parameters.TemplateBehaviors.Add(new TestBehavior());

                // Override the listener node's default settings, so that it will not ask for witness data in P2P messages.
                listenerConnMan.Parameters.Services &= ~NetworkPeerServices.NODE_WITNESS;

                TestHelper.Connect(listener, node);

                // Mine a Segwit block on the first node. It should have commitment data as its settings have not been modified.
                var script = new Key().PubKey.WitHash.ScriptPubKey;
                var miner = node.FullNode.NodeService<IPowMining>() as PowMining;
                List<uint256> res = miner.GenerateBlocks(new ReserveScript(script), 1, int.MaxValue);
                Block block = node.FullNode.ChainIndexer.GetHeader(res.First()).Block;
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node.FullNode.Network, block);
                Assert.NotNull(commitment);

                // The listener should sync the mined block without validation failures.
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetBlockCount() >= 1, cancellationToken: cancellationToken);

                // We need to capture a message on the non-witness-enabled destination node and see that it contains a block serialised without witness data.
                INetworkPeer connectedPeer = listenerConnMan.ConnectedPeers.FindByEndpoint(node.Endpoint);
                TestBehavior testBehavior = connectedPeer.Behavior<TestBehavior>();

                var blockMessages = testBehavior.receivedMessageTracker["block"];
                var blockReceived = blockMessages.First();

                var receivedBlock = blockReceived.Message.Payload as BlockPayload;
                var parsedBlock = receivedBlock.Obj;

                // The block mined on the mining node (witness) should be bigger than the one received by the listener (no witness).
                Assert.True(block.GetSerializedSize() > parsedBlock.GetSerializedSize());

                // Reserialise the received block without witness data (this should have no effect on its size).
                var nonWitnessBlock = parsedBlock.WithOptions(listener.FullNode.Network.Consensus.ConsensusFactory, TransactionOptions.None);

                // We received a block without witness data in the first place.
                Assert.True(parsedBlock.GetSerializedSize() == nonWitnessBlock.GetSerializedSize());
            }
        }

        [Fact]
        public void SegwitWalletTransactionBuildingAndPropagationTest()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();
                CoreNode listener = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                IConnectionManager listenerConnMan = listener.FullNode.NodeService<IConnectionManager>();
                listenerConnMan.Parameters.TemplateBehaviors.Add(new TestBehavior());

                // The listener node will have default settings, i.e. it should ask for witness data in P2P messages.
                Assert.True(listenerConnMan.Parameters.Services.HasFlag(NetworkPeerServices.NODE_WITNESS));

                TestHelper.Connect(listener, node);

                var mineAddress = node.FullNode.WalletManager().GetUnusedAddress();

                var miner = node.FullNode.NodeService<IPowMining>() as PowMining;
                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), (ulong)(node.FullNode.Network.Consensus.CoinbaseMaturity + 1), int.MaxValue);

                // Send a transaction from first node to itself so that it has a proper segwit input to spend.
                var destinationAddress = node.FullNode.WalletManager().GetUnusedAddress();
                var witAddress = destinationAddress.Bech32Address;

                IActionResult transactionResult = node.FullNode.NodeController<WalletController>()
                    .BuildTransaction(new BuildTransactionRequest
                    {
                        AccountName = "account 0",
                        AllowUnconfirmed = true,
                        Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = witAddress, Amount = Money.Coins(1).ToString() } },
                        Password = node.WalletPassword,
                        WalletName = node.WalletName,
                        FeeAmount = Money.Coins(0.001m).ToString()
                    });

                var walletBuildTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;

                node.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(walletBuildTransactionModel.Hex));

                Transaction witFunds = node.FullNode.Network.CreateTransaction(walletBuildTransactionModel.Hex);
                uint witIndex = witFunds.Outputs.AsIndexedOutputs().First(o => o.TxOut.ScriptPubKey.IsScriptType(ScriptType.P2WPKH)).N;

                TestBase.WaitLoop(() => listener.CreateRPCClient().GetBlockCount() >= 1, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                INetworkPeer connectedPeer = listenerConnMan.ConnectedPeers.FindByEndpoint(node.Endpoint);
                TestBehavior testBehavior = connectedPeer.Behavior<TestBehavior>();

                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), 1, int.MaxValue);

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                // Make sure wallet is synced.
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() == node.FullNode.WalletManager().LastBlockHeight(), cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                // We need to capture a message on the witness-enabled destination node and see that it contains a transaction serialised with witness data.
                // However, the first transaction has no witness data since it was only being sent to a segwit scriptPubKey (i.e. no witness input data).
                // So clear all messages for now.
                testBehavior.receivedMessageTracker.Clear();

                // Send a transaction that has a segwit input, to a segwit address.
                transactionResult = node.FullNode.NodeController<WalletController>()
                    .BuildTransaction(new BuildTransactionRequest
                    {
                        AccountName = "account 0",
                        AllowUnconfirmed = true,
                        Outpoints = new List<OutpointRequest>() { new OutpointRequest() { Index = (int)witIndex, TransactionId = witFunds.GetHash().ToString() } },
                        Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = witAddress, Amount = Money.Coins(0.5m).ToString() } },
                        Password = node.WalletPassword,
                        WalletName = node.WalletName,
                        FeeAmount = Money.Coins(0.001m).ToString()
                    });

                walletBuildTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;

                node.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(walletBuildTransactionModel.Hex));

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                var txMessages = testBehavior.receivedMessageTracker["tx"];
                var txMessage = txMessages.First();

                var receivedTransaction = txMessage.Message.Payload as TxPayload;
                var parsedTransaction = receivedTransaction.Obj;
                var nonWitnessTransaction = parsedTransaction.WithOptions(TransactionOptions.None, listener.FullNode.Network.Consensus.ConsensusFactory);

                Assert.True(parsedTransaction.GetSerializedSize() > nonWitnessTransaction.GetSerializedSize());

                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), 1, int.MaxValue);

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
            }
        }

        [Fact]
        public void SegwitWalletTransactionBuildingTest_SpendP2WPKHAndNormalUTXOs()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();
                CoreNode listener = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                TestHelper.Connect(listener, node);

                var mineAddress = node.FullNode.WalletManager().GetUnusedAddress();

                int maturity = (int)node.FullNode.Network.Consensus.CoinbaseMaturity;

                var miner = node.FullNode.NodeService<IPowMining>() as PowMining;
                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), (ulong)(maturity + 2), int.MaxValue);

                // Send a transaction from first node to itself so that it has a proper segwit input to spend.
                var destinationAddress = node.FullNode.WalletManager().GetUnusedAddress();
                var witAddress = destinationAddress.Bech32Address;

                var p2wpkhAmount = Money.Coins(1);

                IActionResult transactionResult = node.FullNode.NodeController<WalletController>()
                    .BuildTransaction(new BuildTransactionRequest
                    {
                        AccountName = "account 0",
                        AllowUnconfirmed = true,
                        Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = witAddress, Amount = p2wpkhAmount.ToString() } },
                        Password = node.WalletPassword,
                        WalletName = node.WalletName,
                        FeeAmount = Money.Coins(0.001m).ToString()
                    });

                var walletBuildTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;

                node.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(walletBuildTransactionModel.Hex));

                Transaction witFunds = node.FullNode.Network.CreateTransaction(walletBuildTransactionModel.Hex);
                uint witIndex = witFunds.Outputs.AsIndexedOutputs().First(o => o.TxOut.ScriptPubKey.IsScriptType(ScriptType.P2WPKH)).N;

                TestBase.WaitLoop(() => listener.CreateRPCClient().GetBlockCount() >= (maturity + 2), cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), 1, int.MaxValue);

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                // Make sure wallet is synced.
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() == node.FullNode.WalletManager().LastBlockHeight(), cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                var spendable = node.FullNode.WalletManager().GetSpendableTransactionsInWallet(node.WalletName).Where(t => t.Address.Bech32Address != witAddress);

                // By sending more than the size of the P2WPKH UTXO, we guarantee that at least one non-P2WPKH UTXO gets included
                transactionResult = node.FullNode.NodeController<WalletController>()
                    .BuildTransaction(new BuildTransactionRequest
                    {
                        AccountName = "account 0",
                        AllowUnconfirmed = true,
                        Outpoints = new List<OutpointRequest>() {
                            new OutpointRequest() { Index = (int)witIndex, TransactionId = witFunds.GetHash().ToString() }, 
                            new OutpointRequest() { Index = spendable.First().Transaction.Index, TransactionId = spendable.First().Transaction.Id.ToString() }
                        },
                        Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = witAddress, Amount = (p2wpkhAmount + Money.Coins(0.5m)).ToString() } },
                        Password = node.WalletPassword,
                        WalletName = node.WalletName,
                        FeeAmount = Money.Coins(0.001m).ToString()
                    });

                walletBuildTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;

                node.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(walletBuildTransactionModel.Hex));

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), 1, int.MaxValue);

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
            }
        }

        [Fact]
        public void SegwitWalletTransactionBuildingTest_SendToBech32AndNormalDestination()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();
                CoreNode listener = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                TestHelper.Connect(listener, node);

                var mineAddress = node.FullNode.WalletManager().GetUnusedAddress();

                int maturity = (int)node.FullNode.Network.Consensus.CoinbaseMaturity;

                var miner = node.FullNode.NodeService<IPowMining>() as PowMining;
                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), (ulong)(maturity + 1), int.MaxValue);

                var destinationAddress = node.FullNode.WalletManager().GetUnusedAddress();
                var witAddress = destinationAddress.Bech32Address;
                var nonWitAddress = destinationAddress.Address;

                IActionResult transactionResult = node.FullNode.NodeController<WalletController>()
                    .BuildTransaction(new BuildTransactionRequest
                    {
                        AccountName = "account 0",
                        AllowUnconfirmed = true,
                        Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = witAddress, Amount = Money.Coins(1).ToString() } },
                        Password = node.WalletPassword,
                        WalletName = node.WalletName,
                        FeeAmount = Money.Coins(0.001m).ToString()
                    });

                var walletBuildTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;

                node.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(walletBuildTransactionModel.Hex));

                Transaction witFunds = node.FullNode.Network.CreateTransaction(walletBuildTransactionModel.Hex);
                uint witIndex = witFunds.Outputs.AsIndexedOutputs().First(o => o.TxOut.ScriptPubKey.IsScriptType(ScriptType.P2WPKH)).N;

                TestBase.WaitLoop(() => listener.CreateRPCClient().GetBlockCount() >= (maturity + 1), cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), 1, int.MaxValue);

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                // Make sure wallet is synced.
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() == node.FullNode.WalletManager().LastBlockHeight(), cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                // By sending more than the size of the P2WPKH UTXO, we guarantee that at least one non-P2WPKH UTXO gets included
                transactionResult = node.FullNode.NodeController<WalletController>()
                    .BuildTransaction(new BuildTransactionRequest
                    {
                        AccountName = "account 0",
                        AllowUnconfirmed = true,
                        Outpoints = new List<OutpointRequest>() { new OutpointRequest() { Index = (int)witIndex, TransactionId = witFunds.GetHash().ToString() } },
                        Recipients = new List<RecipientModel>
                        {
                            new RecipientModel { DestinationAddress = witAddress, Amount = Money.Coins(0.4m).ToString() },
                            new RecipientModel { DestinationAddress = nonWitAddress, Amount = Money.Coins(0.4m).ToString() }
                        },
                        Password = node.WalletPassword,
                        WalletName = node.WalletName,
                        FeeAmount = Money.Coins(0.001m).ToString()
                    });

                walletBuildTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;

                node.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(walletBuildTransactionModel.Hex));

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length > 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);

                miner.GenerateBlocks(new ReserveScript(mineAddress.ScriptPubKey), 1, int.MaxValue);

                TestBase.WaitLoop(() => node.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
                TestBase.WaitLoop(() => listener.CreateRPCClient().GetRawMempool().Length == 0, cancellationToken: new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
            }
        }

        [Fact]
        public void StakeSegwitBlock_On_SBFN_Check_StratisX_Syncs_When_SBFN_Initiates_Connection()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Even though we are mining, we still want to use PoS consensus rules.
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                // Need the premine to be past coinbase maturity so that we can stake with it.
                RPCClient rpc = node.CreateRPCClient();
                rpc.Generate(12);

                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() >= 12, cancellationToken: cancellationToken);

                // Now need to start staking.
                var staker = node.FullNode.NodeService<IPosMinting>() as PosMinting;

                staker.Stake(new WalletSecret()
                {
                    WalletName = node.WalletName,
                    WalletPassword = node.WalletPassword
                });

                // Wait for the chain height to increase.
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);

                // Get the first staked block.
                Block block = node.FullNode.ChainIndexer.GetHeader(13).Block;

                // Confirm that the staked block is Segwit-ted.
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node.FullNode.Network, block);

                // We presume that the consensus rules are checking the actual validity of the commitment, we just ensure that it exists here.
                Assert.NotNull(commitment);

                CoreNode stratisX = builder.CreateStratisXNode().Start();

                // Now connect the stratisX node and allow it to sync.
                node.CreateRPCClient().AddNode(stratisX.Endpoint);

                TestBase.WaitLoop(() => stratisX.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);
            }
        }

        [Fact]
        public void StakeSegwitBlock_On_SBFN_Check_StratisX_Syncs_When_StratisX_Initiates_Connection()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Even though we are mining, we still want to use PoS consensus rules.
                CoreNode node = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                // Need the premine to be past coinbase maturity so that we can stake with it.
                RPCClient rpc = node.CreateRPCClient();
                rpc.Generate(12);

                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() >= 12, cancellationToken: cancellationToken);

                // Now need to start staking.
                var staker = node.FullNode.NodeService<IPosMinting>() as PosMinting;

                staker.Stake(new WalletSecret()
                {
                    WalletName = node.WalletName,
                    WalletPassword = node.WalletPassword
                });

                // Wait for the chain height to increase.
                TestBase.WaitLoop(() => node.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);

                // Get the first staked block.
                Block block = node.FullNode.ChainIndexer.GetHeader(13).Block;

                // Confirm that the staked block is Segwit-ted.
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node.FullNode.Network, block);

                // We presume that the consensus rules are checking the actual validity of the commitment, we just ensure that it exists here.
                Assert.NotNull(commitment);

                // stratisX appears to have problems with addnode RPC calls sent after the node has started up, so set the endpoint in the config instead.
                var parameters = new NodeConfigParameters { { "addnode", node.Endpoint.ToString() } };

                // Start the stratisX node and allow it to sync.
                // The P2P behaviours can have asymmetric rules about version requirements, so we have to test in both directions.
                CoreNode stratisX = builder.CreateStratisXNode(configParameters: parameters).Start();

                TestBase.WaitLoop(() => stratisX.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);
            }
        }

        [Fact]
        public void StakeSegwitBlock_On_SBFN_Connected_To_SBFN_Check_StratisX_Syncs_When_StratisX_Connects_Later()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();
                CoreNode node2 = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                // Need the premine to be past coinbase maturity so that we can stake with it.
                RPCClient rpc = node1.CreateRPCClient();
                rpc.Generate(12);

                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
                TestBase.WaitLoop(() => node1.CreateRPCClient().GetBlockCount() >= 12, cancellationToken: cancellationToken);

                TestHelper.ConnectAndSync(node1, node2);

                // Now need to start staking on one of the SBFN nodes.
                var staker = node1.FullNode.NodeService<IPosMinting>() as PosMinting;

                staker.Stake(new WalletSecret()
                {
                    WalletName = node1.WalletName,
                    WalletPassword = node1.WalletPassword
                });

                // Wait for the chain height to increase.
                TestBase.WaitLoop(() => node1.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);

                // Get the first staked block.
                Block block = node1.FullNode.ChainIndexer.GetHeader(13).Block;

                // Confirm that the staked block is Segwit-ted.
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node1.FullNode.Network, block);

                Assert.NotNull(commitment);

                // Wait for the other SBFN node to sync.
                TestBase.WaitLoop(() => node2.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);

                // The idea here is that the second SBFN node has received and validated a segwit block from node1. So it should now be expecting witness data from its other peers.
                // However, for this test we only sync stratisX rather than allow it to mine.
                var parameters = new NodeConfigParameters { { "addnode", node2.Endpoint.ToString() } };

                // Start the stratisX node and allow it to sync.
                // The P2P behaviours can have asymmetric rules about version requirements, so we have to test in both directions.
                CoreNode stratisX = builder.CreateStratisXNode(configParameters: parameters).Start();

                TestBase.WaitLoop(() => stratisX.CreateRPCClient().GetBlockCount() >= 13, cancellationToken: cancellationToken);
            }
        }

        [Fact]
        public void Mine_On_StratisX_SBFN_Syncs_Via_Intermediary_Gateway_Node()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this).WithLogsEnabled())
            {
                CoreNode stratisX = builder.CreateStratisXNode().Start();

                // Generate some blocks on the X node
                RPCClient rpc = stratisX.CreateRPCClient();
                rpc.SendCommand(RPCOperations.generate, 12);

                // Node 1 is the SBFN gateway node
                var callback = new Action<IFullNodeBuilder>(build => build
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC());

                var config = new NodeConfigParameters
                {
                    {"whitebind", "0.0.0.0"},
                    {"gateway", "1"}
                };

                CoreNode node1 = builder
                    .CreateCustomNode(callback, KnownNetworks.StratisRegTest, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, minProtocolVersion: ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: config)
                    .WithWallet().Start();

                // Connect inbound to the gateway node so that the X node isn't disconnected for not being able to provide witness data
                // This test should not have an outbound connection to stratisX in order to simulate a 'real' environment
                // Addnode does not work properly with stratisX, because it only attempts connections to nodes added this way every 2 minutes.
                rpc.AddNode(node1.Endpoint);

                // We have to wait up to 3 minutes here
                var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(3)).Token;
                TestBase.WaitLoop(() => node1.CreateRPCClient().GetBlockCount() >= 12, cancellationToken: cancellationToken);

                // Node 2 is the 'ordinary' SBFN node
                CoreNode node2 = builder.CreateStratisPosNode(KnownNetworks.StratisRegTest).WithWallet().Start();

                TestHelper.ConnectAndSync(node1, node2);

                // Get the last received block from node2 after it syncs
                Block block = node1.FullNode.ChainIndexer.GetHeader(12).Block;

                // The block will not have a witness commitment, as it was mined by stratisX
                Script commitment = WitnessCommitmentsRule.GetWitnessCommitment(node1.FullNode.Network, block);

                Assert.Null(commitment);
            }
        }
    }
}
