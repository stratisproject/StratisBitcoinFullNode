using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerTests
    {
        private readonly Network powNetwork;

        public ConsensusManagerTests()
        {
            this.powNetwork = new BitcoinRegTest();
        }

        private class ConsensusOptionsTest : PosConsensusOptions
        {
            public ConsensusOptionsTest() : base(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5)
            {
            }

            public override int GetStakeMinConfirmations(int height, Network network)
            {
                return height < 55 ? 50 : 60;
            }
        }

        public class StratisConsensusOptionsOverrideTest : StratisRegTest
        {
            public StratisConsensusOptionsOverrideTest()
            {
                this.Name = Guid.NewGuid().ToString();
            }
        }

        public class BitcoinMaxReorgOverrideTest : BitcoinRegTest
        {
            public BitcoinMaxReorgOverrideTest()
            {
                this.Name = Guid.NewGuid().ToString();

                Type consensusType = typeof(NBitcoin.Consensus);
                consensusType.GetProperty("MaxReorgLength").SetValue(this.Consensus, (uint)20);
            }
        }

        public class BitcoinOverrideRegTest : BitcoinRegTest
        {
            public BitcoinOverrideRegTest() : base()
            {
                this.Name = Guid.NewGuid().ToString();
            }
        }

        public class FailValidation : FullValidationConsensusRule
        {
            private readonly int failheight;
            private int failcount;

            public FailValidation(int failheight, int failcount = 1)
            {
                this.failheight = failheight;
                this.failcount = failcount;
            }

            public override Task RunAsync(RuleContext context)
            {
                if (this.failcount > 0)
                {
                    if (context.ValidationContext.ChainedHeaderToValidate.Height == this.failheight)
                    {
                        this.failcount -= 1;
                        throw new ConsensusErrorException(new ConsensusError("error", "error"));
                    }
                }

                return Task.CompletedTask;
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Reorgs_AndResyncs_ToBestHeight()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork).Start();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Disconnect Miner A and B.
                TestHelper.Disconnect(syncer, minerA);
                TestHelper.Disconnect(syncer, minerB);

                // Ensure syncer does not have any connections.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(syncer));

                // Miner A continues to mine to height 20 whilst disconnected.
                TestHelper.MineBlocks(minerA, 10);

                // Miner B continues to mine to height 14 whilst disconnected.
                TestHelper.MineBlocks(minerB, 4);

                // Syncer now connects to both miners causing a re-org to occur for Miner B back to height 10
                TestHelper.Connect(minerA, syncer);
                TestHelper.Connect(minerB, minerA);

                // Ensure that Syncer has synced with Miner A and Miner B.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(minerA, syncer));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(minerB, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 20);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == 20);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 20);
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Gets_Disconnected_Due_To_InvalidStakeDepth()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisConsensusOptionsOverrideTest();

                // MinerA requires an physical wallet to stake with.
                var minerA = builder.CreateStratisPosNode(network, "minerA").OverrideDateTimeProvider().WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(network, "minerB").OverrideDateTimeProvider().Start();
                var syncer = builder.CreateStratisPosNode(network, "syncer").OverrideDateTimeProvider().Start();

                // MinerA mines to height 55.
                TestHelper.MineBlocks(minerA, 55);

                // Sync the network to height 55.
                TestHelper.ConnectAndSync(syncer, minerA);
                TestHelper.ConnectAndSync(syncer, minerB);

                // Disconnect Miner A and B.
                TestHelper.DisconnectAll(syncer, minerA, minerB);

                // Miner A stakes a coin that increases the network height to 56.
                var minter = minerA.FullNode.NodeService<IPosMinting>();
                minter.Stake(new WalletSecret() { WalletName = "mywallet", WalletPassword = "password" });

                TestHelper.WaitLoop(() =>
                {
                    return minerA.FullNode.ConsensusManager().Tip.Height == 56;
                });

                minter.StopStake();

                // Update the network consensus options so that the GetStakeMinConfirmations returns a higher value
                // to ensure that the InvalidStakeDepth exception can be thrown.
                minerA.FullNode.Network.Consensus.Options = new ConsensusOptionsTest();
                minerB.FullNode.Network.Consensus.Options = new ConsensusOptionsTest();
                syncer.FullNode.Network.Consensus.Options = new ConsensusOptionsTest();

                // Syncer now connects to both miners causing a InvalidStakeDepth exception to be thrown
                // on Miner A.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);

                // Wait until syncer has disconnected either minerA or minerB due to a InvalidStakeDepth exception.
                TestHelper.WaitLoop(() => syncer.FullNode.ConnectionManager.ConnectedPeers.Where(p => !p.Inbound).Count() == 1);

                // Determine which node was disconnected.
                CoreNode survived = null;
                CoreNode notSurvived = null;
                if (TestHelper.IsNodeConnectedTo(syncer, minerA))
                {
                    survived = minerA;
                    notSurvived = minerB;
                }
                else
                {
                    survived = minerB;
                    notSurvived = minerA;
                }

                // Ensure that Syncer is synced with MinerB.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, survived));

                // Ensure that Syncer is not connected to MinerA.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, notSurvived));

                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 55);
                Assert.True(survived.FullNode.ConsensusManager().Tip.Height == 55);
            }
        }

        [Retry]
        public void ConsensusManager_Fork_Occurs_Node_Gets_Disconnected_Due_To_MaxReorgViolation()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new BitcoinMaxReorgOverrideTest();

                var minerA = builder.CreateStratisPowNode(network).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(network).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(network).Start();

                // MinerA mines to height 20.
                TestHelper.MineBlocks(minerA, 20);

                // Sync the network to height 20.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Disconnect Miner A and B.
                TestHelper.Disconnect(syncer, minerA);
                TestHelper.Disconnect(syncer, minerB);

                // Ensure syncer does not have any connections.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(syncer));

                // MinerA continues to mine to height 45.
                TestHelper.MineBlocks(minerA, 25);

                // MinerB continues to mine to height 65.
                TestHelper.MineBlocks(minerB, 45);

                // Syncer now connects to both miners causing a MaxReorgViolation exception to be thrown
                // on Miner B.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);

                // Determine which node was disconnected.
                CoreNode survived = null;
                CoreNode notSurvived = null;
                if (TestHelper.IsNodeConnectedTo(syncer, minerA))
                {
                    survived = minerA;
                    notSurvived = minerB;
                }
                else
                {
                    survived = minerB;
                    notSurvived = minerA;
                }

                // Ensure that Syncer is synced with MinerB.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, survived));

                // Ensure that Syncer is not connected to MinerA.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, notSurvived));

                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 45);
                Assert.True(survived.FullNode.ConsensusManager().Tip.Height == 45);

            }
        }

        [Fact]
        public void ConsensusManager_Reorgs_Then_Old_Chain_Becomes_Longer_Then_Reorg_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork).Start();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Enable syncer to send blocks to miner B
                TestHelper.EnableBlockPropagation(syncer, minerB);

                // Disable syncer from sending blocks to miner A
                TestHelper.DisableBlockPropagation(syncer, minerA);

                // Miner B continues to mine to height 30 on a new and longer chain whilst disconnected.
                TestHelper.MineBlocks(minerB, 20);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Enable syncer to send blocks to miner B
                TestHelper.EnableBlockPropagation(syncer, minerA);

                // Miner A mines to height 40.
                TestHelper.MineBlocks(minerA, 20);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 40);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == 40);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 40);
            }
        }

        [Fact]
        public void ConsensusManager_Reorgs_Then_Try_To_Connect_Longer_Chain_With_Connected_Blocks_And_Fail_Then_Revert_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork).Start();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == 20);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 10);
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 20);

                // Inject a rule that will fail at block 15 of the new chain.
                var engine = syncer.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                syncerNetwork.Consensus.FullValidationRules.Insert(1, new FailValidation(15));
                engine.Register();

                // Miner B continues to mine to height 30 on a new and longer chain.
                TestHelper.MineBlocks(minerB, 20);

                // check miner B at height 30.
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 30);

                // Miner B should become disconnected.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back.
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 20);

                // Check syncer is still synced with Miner A.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void ConsensusManager_Reorgs_Then_Try_To_Connect_Longer_Chain_With_No_Connected_Blocks_And_Fail_Then_Revert_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork).Start();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Inject a rule that will fail at block 11 of the new chain
                ConsensusRuleEngine engine = syncer.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                syncerNetwork.Consensus.FullValidationRules.Insert(1, new FailValidation(11));
                engine.Register();

                // Miner B continues to mine to height 30 on a new and longer chain.
                TestHelper.MineBlocks(minerB, 20);

                // check miner B at height 30.
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 30);

                // Miner B should become disconnected.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 20);

                // Check syncer is still synced with Miner A
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void ConsensusManager_Reorg_To_Longest_Chain_Multiple_Times_Without_Invalid_Blocks()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork);

                void flushCondition(IServiceCollection services)
                {
                    ServiceDescriptor service = services.FirstOrDefault(s => s.ServiceType == typeof(IBlockStoreQueueFlushCondition));
                    if (service != null)
                        services.Remove(service);

                    services.AddSingleton<IBlockStoreQueueFlushCondition>((serviceprovider) =>
                    {
                        var chainState = serviceprovider.GetService<IChainState>();
                        return new BlockStoreQueueFlushConditionReorgTests(chainState, 10);
                    });
                };

                syncer.OverrideService(flushCondition).Start();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                TestHelper.DisableBlockPropagation(syncer, minerA);
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Syncer syncs to minerA's block of 11
                TestHelper.MineBlocks(minerA, 1);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 11);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 11);

                // Syncer jumps chain and reorgs to minerB's longer chain of 12
                TestHelper.MineBlocks(minerB, 2);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 11);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 12);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 12);

                // Syncer jumps chain and reorg to minerA's longer chain of 18
                TestHelper.MineBlocks(minerA, 2);
                TestHelper.TriggerSync(syncer);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 13);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 12);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 13);
            }
        }

        [Fact]
        public void ConsensusManager_Connect_New_Block_Failed()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork).Start();

                // Miner A mines to height 11.
                TestHelper.MineBlocks(minerA, 11);

                // Inject a rule that will fail at block 11 of the new chain
                ConsensusRuleEngine engine = syncer.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                syncerNetwork.Consensus.FullValidationRules.Insert(1, new FailValidation(11));
                engine.Register();

                // Connect syncer to Miner A, reorg should fail.
                TestHelper.Connect(syncer, minerA);

                // Syncer should disconnect from miner A after the failed block.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerA));

                // Make sure syncer rolled back
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 10);
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Of_100_Blocks_Occurs_Node_Reorgs_And_Resyncs_ToBestHeight()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork).Start();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                TestHelper.DisableBlockPropagation(syncer, minerA);
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A mines 105 blocks to height 115.
                TestHelper.MineBlocks(minerA, 105);
                TestHelper.WaitForNodeToSync(syncer, minerA);

                // Miner B continues mines 110 blocks to a longer chain at height 120.
                TestHelper.MineBlocks(minerB, 110);
                TestHelper.WaitForNodeToSync(syncer, minerB);

                // Miner A mines an additional 10 blocks to height 125 that will create the longest chain.
                TestHelper.MineBlocks(minerA, 10);
                TestHelper.WaitForNodeToSync(syncer, minerA);

                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 125);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == 125);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 120);
            }
        }


        [Fact(Skip = "Work in progress")]
        public void ConsensusManager_Fork_Occurs_When_Stake_Coins_Are_Spent_And_Found_In_Rewind_Data()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                // MinerA requires an physical wallet to stake with.
                var minerA = builder.CreateStratisPosNode(network, "minerA").OverrideDateTimeProvider().WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(network, "minerB").OverrideDateTimeProvider().WithWallet().Start();

                // MinerA mines to height CoinbaseMaturity.
                HdAddress minerAAddress = TestHelper.MineBlocks(minerA, (int)network.Consensus.CoinbaseMaturity).AddressUsed;

                // Sync the network to height CoinbaseMaturity.
                TestHelper.ConnectAndSync(minerA, minerB);

                // Disconnect Miner A and B.
                TestHelper.DisconnectAll(minerA, minerB);

                // MinerA mines 2 blocks on its own fork.
                TestHelper.MineBlocks(minerA, 2);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == network.Consensus.CoinbaseMaturity + 2);

                //before staking, create a transaction that will spend the coin used to stake
                Transaction txThatSpendCoinstake = CreateTransactionThatWillSpendCoinstake(network, minerA, minerB);

                // Miner A stakes one coin.
                var minterA = minerA.FullNode.NodeService<IPosMinting>();
                minterA.Stake(new WalletSecret() { WalletName = "mywallet", WalletPassword = "password" });

                TestHelper.WaitLoop(() =>
                {
                    return minerA.FullNode.ConsensusManager().Tip.Height == network.Consensus.CoinbaseMaturity + 3;
                });

                minterA.StopStake();

                var posBlock = minerA.FullNode.ConsensusManager().Tip.Block as PosBlock;
                Assert.True(posBlock != null);

                var coinstakeTransactionA = posBlock.GetProtocolTransaction();
                Assert.True(coinstakeTransactionA.IsCoinStake);



                // MinerB mines 5 blocks on its own fork.
                TestHelper.MineBlocks(minerB, 1);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == network.Consensus.CoinbaseMaturity + 1);

                // Use the coinstake transaction to spend it on the minerB fork
                TxIn coinstake = coinstakeTransactionA.Inputs[0];

                // coinstakeTransactionA.Inputs.Remove(coinstakeTransactionA.Inputs)

                // Transaction txThatSpendCoinstake = CreateTransactionThatSpendCoinstake2(network, minerA, minerB, coinstakeTransactionA, coinstake);
                txThatSpendCoinstake = UpdateTransactionThatWillSpendCoinstake(minerA, txThatSpendCoinstake);
                Assert.True(minerB.AddToStratisMempool(txThatSpendCoinstake));

                // Wait for the transaction to be picked up by the mempool
                TestHelper.WaitLoop(() => minerB.CreateRPCClient().GetRawMempool().Length > 0);

                // MinerB mines 1 blocks on its own fork.
                TestHelper.MineBlocks(minerB, 1);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == network.Consensus.CoinbaseMaturity + 2);
                var powBlockWithSpentCoinstake = minerB.FullNode.ConsensusManager().Tip.Block; // TODO: I'd like to mine a block including the txThatSpendCoinstake here

                // Sync the network, minerA should switch to minerB.
                TestHelper.MineBlocks(minerB, 3);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == network.Consensus.CoinbaseMaturity + 5);

                var expectedValidChainHeight = minerB.FullNode.ConsensusManager().Tip.Height;

                // Sync the network to height CoinbaseMaturity.
                TestHelper.ConnectAndSync(minerA, minerB);

                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == expectedValidChainHeight);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == expectedValidChainHeight);
            }
        }

        private static Transaction CreateTransactionThatSpendCoinstake(StratisRegTest network, CoreNode minerA, CoreNode minerB, TxIn coinstake)
        {
            Transaction txThatSpendCoinstake = network.CreateTransaction();
            txThatSpendCoinstake.AddInput(coinstake);
            txThatSpendCoinstake.AddOutput(new TxOut
            {
                Value = Money.FromUnit(3, MoneyUnit.BTC),
                ScriptPubKey = minerB.FullNode.WalletManager().GetUnusedAddress().ScriptPubKey
            });

            List<ICoin> coins = new List<ICoin>();
            foreach (var txin in txThatSpendCoinstake.Inputs)
            {
                coins.Add(new Coin(txin.PrevOut, new TxOut()
                {
                    ScriptPubKey = txin.ScriptSig
                }));
            }

            txThatSpendCoinstake.Sign(minerA.FullNode.Network, minerA.MinerSecret, coins[0]);

            return txThatSpendCoinstake;
        }
        private static Transaction CreateTransactionThatSpendCoinstake2(StratisRegTest network, CoreNode minerA, CoreNode minerB, Transaction coinstakeTransaction, TxIn coinstake)
        {
            var txBuildContext = new TransactionBuildContext(minerA.FullNode.Network)
            {
                AccountReference = new WalletAccountReference("mywallet", "account 0"),
                ChangeAddress = minerA.FullNode.WalletManager().GetUnusedAddress(),
                MinConfirmations = (int)network.Consensus.CoinbaseMaturity,
                FeeType = FeeType.High,
                WalletPassword = "password",
                Recipients = new[] { new Recipient { Amount = 99999, ScriptPubKey = minerB.FullNode.WalletManager().GetUnusedAddress().ScriptPubKey } }.ToList()
            };

            var txThatSpendCoinstake = minerA.FullNode.Network.CreateTransaction();
            txThatSpendCoinstake.Time = (uint)minerA.FullNode.NodeService<IDateTimeProvider>().GetAdjustedTimeAsUnixTimestamp();

            foreach (var txIn in coinstakeTransaction.Inputs)
            {
                txThatSpendCoinstake.AddInput(new TxIn(txIn.PrevOut, minerA.MinerSecret.ScriptPubKey));
            }

            foreach (var txOut in coinstakeTransaction.Outputs)
            {
                txThatSpendCoinstake.AddOutput(new TxOut(txOut.Value, txOut.ScriptPubKey));
            }

            List<ICoin> coins = new List<ICoin>();
            foreach (var txin in txThatSpendCoinstake.Inputs)
            {
                coins.Add(new Coin(txin.PrevOut, new TxOut()
                {
                    ScriptPubKey = txin.ScriptSig
                }));
            }

            txThatSpendCoinstake.Sign(minerA.FullNode.Network, minerA.MinerSecret, coins[0]);

            return txThatSpendCoinstake;
        }

        private static Transaction CreateTransactionThatWillSpendCoinstake(StratisRegTest network, CoreNode minerA, CoreNode minerB)
        {
            var txBuildContext = new TransactionBuildContext(minerA.FullNode.Network)
            {
                AccountReference = new WalletAccountReference("mywallet", "account 0"),
                ChangeAddress = minerA.FullNode.WalletManager().GetUnusedAddress(),
                MinConfirmations = (int)network.Consensus.CoinbaseMaturity,
                FeeType = FeeType.High,
                WalletPassword = "password",
                Recipients = new[] { new Recipient { Amount = 999999, ScriptPubKey = minerB.FullNode.WalletManager().GetUnusedAddress().ScriptPubKey } }.ToList()
            };

            Transaction txThatSpendCoinstake = minerA.FullNode.NodeService<IWalletTransactionHandler>().BuildTransaction(txBuildContext);

            return txThatSpendCoinstake;
        }

        private Transaction UpdateTransactionThatWillSpendCoinstake(CoreNode scSender, Transaction txThatSpendCoinstake)
        {
            var updatedTransaction = scSender.FullNode.Network.CreateTransaction();
            updatedTransaction.Time = (uint)scSender.FullNode.NodeService<IDateTimeProvider>().GetAdjustedTimeAsUnixTimestamp();

            foreach (var txIn in txThatSpendCoinstake.Inputs)
            {
                updatedTransaction.AddInput(new TxIn(txIn.PrevOut, scSender.MinerSecret.ScriptPubKey));
            }

            foreach (var txOut in txThatSpendCoinstake.Outputs)
            {
                updatedTransaction.AddOutput(new TxOut(txOut.Value, txOut.ScriptPubKey));
            }

            List<ICoin> coins = new List<ICoin>();
            foreach (var txin in updatedTransaction.Inputs)
            {
                coins.Add(new Coin(txin.PrevOut, new TxOut()
                {
                    ScriptPubKey = txin.ScriptSig
                }));
            }

            updatedTransaction.Sign(scSender.FullNode.Network, scSender.MinerSecret, coins[0]);

            return updatedTransaction;
        }
    }

    public class BlockStoreQueueFlushConditionReorgTests : IBlockStoreQueueFlushCondition
    {
        private readonly IChainState chainState;
        private readonly int interceptAtBlockHeight;

        public BlockStoreQueueFlushConditionReorgTests(IChainState chainState, int interceptAtBlockHeight)
        {
            this.chainState = chainState;
            this.interceptAtBlockHeight = interceptAtBlockHeight;
        }

        public bool ShouldFlush
        {
            get
            {
                if (this.chainState.ConsensusTip.Height >= this.interceptAtBlockHeight)
                    return false;

                return this.chainState.IsAtBestChainTip;
            }
        }
    }
}