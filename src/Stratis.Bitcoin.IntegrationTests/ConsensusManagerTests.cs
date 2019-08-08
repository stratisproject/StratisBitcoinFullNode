﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerTests
    {
        private const string Password = "password";
        private const string WalletName = "mywallet";
        private const string Passphrase = "passphrase";
        private const string Account = "account 0";

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

        public class FailValidation15_2 : FailValidation
        {
            public FailValidation15_2() : base(15, 2)
            {
            }
        }

        public class FailValidation11 : FailValidation
        {
            public FailValidation11() : base(11)
            {
            }
        }

        public class FailValidation11_2 : FailValidation
        {
            public FailValidation11_2() : base(11, 2)
            {
            }
        }

        public class FailValidation : FullValidationConsensusRule
        {
            /// <summary>
            /// Fail at this height if <see cref="failOnAttemptCount"/> is zero, otherwise decrement it.
            /// </summary>
            private readonly int failOnHeight;

            /// <summary>
            /// The number of blocks at height <see cref="failOnHeight"/> that need to pass before an error is thrown.
            /// </summary>
            private int failOnAttemptCount;

            public FailValidation(int failOnHeight, int failOnAttemptCount = 1)
            {
                this.failOnHeight = failOnHeight;
                this.failOnAttemptCount = failOnAttemptCount;
            }

            public override Task RunAsync(RuleContext context)
            {
                if (this.failOnAttemptCount > 0)
                {
                    if (context.ValidationContext.ChainedHeaderToValidate.Height == this.failOnHeight)
                    {
                        this.failOnAttemptCount -= 1;

                        if (this.failOnAttemptCount == 0)
                        {
                            throw new ConsensusErrorException(new ConsensusError("ConsensusManagerTests-FailValidation-Error", "ConsensusManagerTests-FailValidation-Error"));
                        }
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
                var minerA = builder.CreateStratisPowNode(this.powNetwork, "cm-1-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork, "cm-1-minerB").WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork, "cm-1-syncer").Start();

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA);
                TestHelper.ConnectAndSync(syncer, minerB);

                // Disconnect Miner A and B.
                TestHelper.Disconnect(syncer, minerA);
                TestHelper.Disconnect(syncer, minerB);

                // Ensure syncer does not have any connections.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnected(syncer));

                // Miner A continues to mine to height 15 whilst disconnected.
                TestHelper.MineBlocks(minerA, 5);

                // Miner B continues to mine to height 12 whilst disconnected.
                TestHelper.MineBlocks(minerB, 2);

                // Syncer now connects to both miners causing a re-org to occur for Miner B back to height 10
                TestHelper.Connect(minerA, syncer);
                TestHelper.Connect(minerB, minerA);

                // Ensure that Syncer has synced with Miner A and Miner B.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(minerA, syncer));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(minerB, minerA));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 15));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 15));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 15));
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Gets_Disconnected_Due_To_InvalidStakeDepth()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisConsensusOptionsOverrideTest();

                // MinerA requires a physical wallet to stake with.
                var minerA = builder.CreateStratisPosNode(network, "cm-2-minerA").OverrideDateTimeProvider().WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(network, "cm-2-minerB").OverrideDateTimeProvider().Start();

                // MinerA mines to height 55.
                TestHelper.MineBlocks(minerA, 55);

                // Connect and sync minerA and minerB.
                TestHelper.ConnectAndSync(minerA, minerB);

                // Disconnect Miner A and B.
                TestHelper.Disconnect(minerA, minerB);

                // Miner A stakes a coin that increases the network height to 56.
                var minter = minerA.FullNode.NodeService<IPosMinting>();
                minter.Stake(new WalletSecret() { WalletName = "mywallet", WalletPassword = "password" });

                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 56));

                minter.StopStake();

                // Update the network consensus options so that the GetStakeMinConfirmations returns a higher value
                // to ensure that the InvalidStakeDepth exception can be thrown.
                minerB.FullNode.Network.Consensus.Options = new ConsensusOptionsTest();

                // Ensure the correct height before the connect.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 56));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 55));

                // Connect minerA to minerB, this will cause an InvalidStakeDepth exception to be thrown on minerB.
                TestHelper.ConnectNoCheck(minerA, minerB);

                // Wait until minerA has disconnected minerB due to the InvalidStakeDepth exception.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 56));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 55));
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Gets_Disconnected_Due_To_MaxReorgViolation()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new BitcoinMaxReorgOverrideTest();

                var minerA = builder.CreateStratisPowNode(network, "cm-3-minerA").WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(network, "cm-3-minerB").WithDummyWallet().Start();

                // MinerA mines height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Connect and sync minerA and minerB.
                TestHelper.ConnectAndSync(minerA, minerB);

                // Disconnect minerA from minerB.
                TestHelper.Disconnect(minerA, minerB);

                // MinerA continues to mine to height 20 (10 + 10).
                TestHelper.MineBlocks(minerA, 10);

                // MinerB continues to mine to height 40 (10 + 30).
                TestHelper.MineBlocks(minerB, 30);

                // Ensure the correct height before the connect.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 20));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 40));

                // Connect minerA to minerB.
                TestHelper.ConnectNoCheck(minerA, minerB);

                // Wait until the nodes become disconnected due to the MaxReorgViolation.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // Check that the heights did not change.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 20));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 40));
            }
        }

        [Fact]
        public void ConsensusManager_Reorgs_Then_Old_Chain_Becomes_Longer_Then_Reorg_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.powNetwork, "cm-4-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork, "cm-4-minerB").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork, "cm-4-syncer").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 15.
                TestHelper.MineBlocks(minerA, 5);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Enable syncer to send blocks to miner B
                TestHelper.EnableBlockPropagation(syncer, minerB);

                // Disable syncer from sending blocks to miner A
                TestHelper.DisableBlockPropagation(syncer, minerA);

                // Miner B continues to mine to height 20 on a new and longer chain whilst disconnected.
                TestHelper.MineBlocks(minerB, 10);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Enable syncer to send blocks to miner B
                TestHelper.EnableBlockPropagation(syncer, minerA);

                // Miner A mines to height 25.
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerA), waitTimeSeconds: 120);
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerB), waitTimeSeconds: 120);

                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 25));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 25));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 25));
            }
        }

        [Fact]
        public void ConsensusManager_Reorgs_Then_Try_To_Connect_Longer_Chain_With_Connected_Blocks_And_Fail_Then_Revert_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                // Inject a rule that will fail at block 15 of the new chain.
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation15_2));

                var minerA = builder.CreateStratisPowNode(this.powNetwork, "cm-5-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork, "cm-5-minerB").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork, "cm-5-syncer").Start();

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 20));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 10));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 20));

                // Miner B continues to mine to height 30 on a new and longer chain.
                TestHelper.MineBlocks(minerB, 20);

                // check miner B at height 30.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 30));

                // Miner B should become disconnected.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back.
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 20);

                // Check syncer is still synced with Miner A.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void ConsensusManager_Reorgs_Then_Try_To_Connect_Longer_Chain_With_No_Connected_Blocks_And_Fail_Then_Revert_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                // Inject a rule that will fail at block 11 of the new chain
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation11_2));

                var minerA = builder.CreateStratisPowNode(this.powNetwork, "cm-6-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork, "cm-6-minerB").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork, "cm-6-syncer").Start();

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Miner B continues to mine to height 30 on a new and longer chain.
                TestHelper.MineBlocks(minerB, 20);

                // check miner B at height 30.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 30));

                // Miner B should become disconnected.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 20));

                // Check syncer is still synced with Miner A
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void ConsensusManager_Reorg_To_Longest_Chain_Multiple_Times_Without_Invalid_Blocks()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.powNetwork, "cm-7-minerA").WithDummyWallet().WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork, "cm-7-minerB").WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork, "cm-7-syncer");

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

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                TestHelper.DisableBlockPropagation(syncer, minerA);
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Syncer syncs to minerA's block of 11
                TestHelper.MineBlocks(minerA, 1);
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 11));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 10));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 11));

                // Syncer jumps chain and reorgs to minerB's longer chain of 12
                TestHelper.MineBlocks(minerB, 2);
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 11));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 12));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 12));

                // Syncer jumps chain and reorg to minerA's longer chain of 18
                TestHelper.MineBlocks(minerA, 2);
                TestHelper.TriggerSync(syncer);
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 13));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 12));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 13));
            }
        }

        [Fact]
        public void ConsensusManager_Connect_New_Block_Failed()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                // Inject a rule that will fail at block 11 of the new chain
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation11));

                var minerA = builder.CreateStratisPowNode(this.powNetwork, "cm-8-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork, "cm-8-syncer").Start();

                // Miner A mines to height 11.
                TestHelper.MineBlocks(minerA, 1);

                // Connect syncer to Miner A, reorg should fail.
                TestHelper.ConnectNoCheck(syncer, minerA);

                // Syncer should disconnect from miner A after the failed block.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerA));

                // Make sure syncer rolled back
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 10));
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Of_100_Blocks_Occurs_Node_Reorgs_And_Resyncs_ToBestHeight()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.powNetwork, "cm-9-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest100Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork, "cm-9-minerB").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest100Listener).Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork, "cm-9-syncer").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest100Listener).Start();

                // Sync the network to height 100.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                TestHelper.DisableBlockPropagation(syncer, minerA);
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A mines 105 blocks to height 115.
                TestHelper.MineBlocks(minerA, 5);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA), waitTimeSeconds: 120);

                // Miner B continues mines 110 blocks to a longer chain at height 120.
                TestHelper.MineBlocks(minerB, 10);
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerB), waitTimeSeconds: 120);

                // Miner A mines an additional 10 blocks to height 125 that will create the longest chain.
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerA), waitTimeSeconds: 120);

                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 115));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 115));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 110));
            }
        }

        /// <summary>This test assumes CoinbaseMaturity is 10 and at block 2 there is a huge premine, adjust the test if this changes.</summary>
        [Fact]
        public void ConsensusManager_Fork_Occurs_When_Stake_Coins_Are_Spent_And_Found_In_Rewind_Data()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                var sharedMnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString();

                // MinerA requires an physical wallet to stake with.
                var minerA = builder.CreateStratisPosNode(network, "cm-10-minerA").OverrideDateTimeProvider().WithWallet(walletMnemonic: sharedMnemonic).Start();
                var minerB = builder.CreateStratisPosNode(network, "cm-10-minerB").OverrideDateTimeProvider().WithWallet(walletMnemonic: sharedMnemonic).Start();

                // MinerA mines 2 blocks to get the big premine coin and mature them (regtest maturity is 10).
                TestHelper.MineBlocks(minerA, 12);

                // Sync the peers A and B (height 3)
                TestHelper.ConnectAndSync(minerA, minerB);

                // Miner A will spend the coins 
                WalletSendTransactionModel walletSendTransactionModel = $"http://localhost:{minerA.ApiPort}/api"
                    .AppendPathSegment("wallet/splitcoins")
                    .PostJsonAsync(new SplitCoinsRequest
                    {
                        WalletName = minerA.WalletName,
                        AccountName = "account 0",
                        WalletPassword = minerA.WalletPassword,
                        TotalAmountToSplit = network.Consensus.PremineReward.ToString(),
                        UtxosCount = 2
                    })
                    .ReceiveJson<WalletSendTransactionModel>().Result;

                TestBase.WaitLoop(() => minerA.FullNode.MempoolManager().InfoAll().Count > 0);
                TestHelper.MineBlocks(minerA, 12);
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 24);
                Assert.Empty(minerA.FullNode.MempoolManager().InfoAll());

                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(minerA, minerB));

                // Disconnect Miner A and B.
                TestHelper.Disconnect(minerA, minerB);

                // Miner A stakes one coin. (height 13)
                var minterA = minerA.FullNode.NodeService<IPosMinting>();
                minterA.Stake(new WalletSecret() { WalletName = "mywallet", WalletPassword = "password" });
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 25);
                minterA.StopStake();

                TestHelper.MineBlocks(minerB, 2); // this will push minerB total work to be highest
                var minterB = minerB.FullNode.NodeService<IPosMinting>();
                minterB.Stake(new WalletSecret() { WalletName = WalletName, WalletPassword = Password });
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 27);
                minterB.StopStake();

                var expectedValidChainHeight = minerB.FullNode.ConsensusManager().Tip.Height;

                // Sync the network, minerA should switch to minerB.
                TestHelper.Connect(minerA, minerB);

                TestHelper.IsNodeSyncedAtHeight(minerA, expectedValidChainHeight);
                TestHelper.IsNodeSyncedAtHeight(minerB, expectedValidChainHeight);
            }
        }

        /// <summary>We test that two chains that used the same UTXO to stake, the shorter chain can still swap to the longer chain.</summary>
        [Fact]
        public void ConsensusManager_Fork_Occurs_When_Stake_Coins_Are_Mined_And_Found_In_Rewind_Data()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                var sharedMnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString();

                // MinerA requires an physical wallet to stake with.
                var minerA = builder.CreateStratisPosNode(network, "cm-10-minerA").OverrideDateTimeProvider().WithWallet(walletMnemonic: sharedMnemonic).Start();
                var minerB = builder.CreateStratisPosNode(network, "cm-10-minerB").OverrideDateTimeProvider().WithWallet(walletMnemonic: sharedMnemonic).Start();

                // MinerA mines 2 blocks to get the big premine coin and mature them (regtest maturity is 10).
                TestHelper.MineBlocks(minerA, 12);

                // Sync the peers A and B (height 12)
                TestHelper.ConnectAndSync(minerA, minerB);

                // Disconnect Miner A and B.
                TestHelper.DisconnectAll(minerA, minerB);

                // Miner A stakes one coin. (height 13)
                var minterA = minerA.FullNode.NodeService<IPosMinting>();
                minterA.Stake(new WalletSecret() { WalletName = "mywallet", WalletPassword = "password" });
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 13);
                minterA.StopStake();

                TestHelper.MineBlocks(minerB, 2); // this will push minerB total work to be highest
                var minterB = minerB.FullNode.NodeService<IPosMinting>();
                minterB.Stake(new WalletSecret() { WalletName = WalletName, WalletPassword = Password });
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 15);
                minterB.StopStake();

                var expectedValidChainHeight = minerB.FullNode.ConsensusManager().Tip.Height;

                // Sync the network, minerA should switch to minerB.
                TestHelper.ConnectAndSync(minerA, minerB);

                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, expectedValidChainHeight));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, expectedValidChainHeight));
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_When_Same_Coins_Are_Staked_On_Different_Chains()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                var minerA = builder.CreateStratisPosNode(network, "cm-11-minerA").OverrideDateTimeProvider().WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(network, "cm-11-minerB").OverrideDateTimeProvider().Start();

                minerB.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase, minerA.Mnemonic);

                var coinbaseMaturity = (int)network.Consensus.CoinbaseMaturity;

                // MinerA mines maturity +2 blocks to get the big premine coin and make it stakable.
                TestHelper.MineBlocks(minerA, coinbaseMaturity + 2);

                // Sync the peers A and B (height 12)
                TestHelper.ConnectAndSync(minerA, minerB);

                // Disconnect Miner A and B.
                TestHelper.DisconnectAll(minerA, minerB);

                // Miner A stakes one coin. (height 13)
                var minterA = minerA.FullNode.NodeService<IPosMinting>();
                var minterAHeigh = minerA.FullNode.ConsensusManager().Tip.Height;
                minterA.Stake(new WalletSecret() { WalletName = WalletName, WalletPassword = Password });
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, minterAHeigh + 1));

                minterA.StopStake();

                var minterB = minerB.FullNode.NodeService<IPosMinting>();
                var minterBHeigh = minerB.FullNode.ConsensusManager().Tip.Height;
                minterB.Stake(new WalletSecret() { WalletName = WalletName, WalletPassword = Password });
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, minterBHeigh + 1));

                minterB.StopStake();

                // MinerB mines 1 block on its own fork. (heightB 13)
                TestHelper.MineBlocks(minerA, 2);
                TestHelper.MineBlocks(minerB, 3);

                TestHelper.ConnectAndSync(minerA, minerB);

                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.HashBlock == minerB.FullNode.ConsensusManager().Tip.HashBlock);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.HashBlock == minerB.FullNode.ConsensusManager().Tip.HashBlock);
            }
        }

        [Fact]
        public void ConsensusManager_Block_That_Failed_Partial_Validation_Is_Rejected()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                // MinerA requires a physical wallet to stake with.
                var minerA = builder.CreateStratisPosNode(network, "minerA").WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(network, "minerB").Start();
                var minerC = builder.CreateStratisPosNode(network, "minerC").Start();

                // MinerA mines to height 5.
                TestHelper.MineBlocks(minerA, 5);

                // Connect and sync minerA and minerB.
                TestHelper.ConnectAndSync(minerA, minerB);

                TestHelper.Disconnect(minerA, minerB);

                // Mark block 5 as invalid by changing the signature of the block in memory.
                (minerB.FullNode.ChainIndexer.GetHeader(5).Block as PosBlock).BlockSignature.Signature = new byte[] { 0 };

                // Connect and sync minerB and minerC.
                TestHelper.ConnectNoCheck(minerB, minerC);

                // TODO: when signaling failed blocks is enabled we should check this here.

                // Wait for the nodes to disconnect due to invalid block.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerB, minerC));

                Assert.True(minerC.FullNode.NodeService<IPeerBanning>().IsBanned(minerB.Endpoint));

                minerC.FullNode.NodeService<IPeerBanning>().UnBanPeer(minerA.Endpoint);

                TestHelper.ConnectAndSync(minerC, minerA);

                TestBase.WaitLoop(() => TestHelper.AreNodesSyncedMessage(minerA, minerC).Passed);
            }
        }

        private static Transaction CreateTransactionThatSpendCoinstake(StratisRegTest network, CoreNode minerA, CoreNode minerB, TxIn coinstake, Transaction txWithBigPremine)
        {
            Transaction txThatSpendCoinstake = network.CreateTransaction();
            txThatSpendCoinstake.AddInput(new TxIn(new OutPoint(txWithBigPremine, 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(minerA.MinerSecret.PubKey)));
            txThatSpendCoinstake.AddOutput(new TxOut
            {
                Value = txWithBigPremine.Outputs[0].Value - new Money(1, MoneyUnit.BTC),
                ScriptPubKey = minerB.MinerHDAddress.ScriptPubKey
            });

            var dateTimeProvider = minerA.FullNode.NodeService<IDateTimeProvider>();
            txThatSpendCoinstake.Time = (uint)dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();


            Coin spentCoin = new Coin(txWithBigPremine, 0);
            List<ICoin> coins = new List<ICoin> { spentCoin };

            txThatSpendCoinstake.Sign(minerA.FullNode.Network, minerA.MinerSecret, coins[0]);

            return txThatSpendCoinstake;
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