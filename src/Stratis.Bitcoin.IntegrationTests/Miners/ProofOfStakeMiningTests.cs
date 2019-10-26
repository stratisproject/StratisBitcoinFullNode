using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public class ProofOfStakeMiningTests
    {
        private class StratisRegTestLastPowBlock : StratisRegTest
        {
            public StratisRegTestLastPowBlock()
            {
                this.Name = Guid.NewGuid().ToString();
            }
        }

        [Fact]
        public void MiningAndPropagatingPOS_MineBlockCheckPeerHasNewBlock()
        {
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                CoreNode node = nodeBuilder.CreateStratisPosNode(network, "posmining-1-node").WithDummyWallet().Start();
                CoreNode syncer = nodeBuilder.CreateStratisPosNode(network, "posmining-1-syncer").Start();

                TestHelper.MineBlocks(node, 1);
                Assert.NotEqual(node.FullNode.ConsensusManager().Tip, syncer.FullNode.ConsensusManager().Tip);

                TestHelper.ConnectAndSync(node, syncer);
                Assert.Equal(node.FullNode.ConsensusManager().Tip, syncer.FullNode.ConsensusManager().Tip);
            }
        }

        [Fact]
        public void MiningAndPropagatingPOS_MineBlockStakeAtInsufficientHeightError()
        {
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTestLastPowBlock();

                CoreNode node = nodeBuilder.CreateStratisPosNode(network, "posmining-2-node").WithDummyWallet().Start();

                // Mine two blocks (OK).
                TestHelper.MineBlocks(node, 2);

                // Mine another block after LastPOWBlock height (Error).
                node.FullNode.Network.Consensus.LastPOWBlock = 2;
                var error = Assert.Throws<ConsensusRuleException>(() => TestHelper.MineBlocks(node, 1));
                Assert.True(error.ConsensusError.Message == ConsensusErrors.ProofOfWorkTooHigh.Message);
            }
        }

        [Fact]
        public void Staking_Wont_Include_Time_Ahead_Of_Coinstake_Timestamp()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var configParameters = new NodeConfigParameters { { "savetrxhex", "true" } };
                var network = new StratisRegTest();

                var minerA = builder.CreateStratisPosNode(network, "stake-1-minerA", configParameters: configParameters).OverrideDateTimeProvider().WithWallet().Start();

                var addressUsed = TestHelper.MineBlocks(minerA, (int)network.Consensus.PremineHeight).AddressUsed;

                // Since the pre-mine will not be immediately spendable, the transactions have to be counted directly from the address.
                addressUsed.Transactions.Count().Should().Be((int)network.Consensus.PremineHeight);

                addressUsed.Transactions.Sum(s => s.Amount).Should().Be(network.Consensus.PremineReward + network.Consensus.ProofOfWorkReward);

                // Mine blocks to maturity.
                TestHelper.MineBlocks(minerA, (int)network.Consensus.CoinbaseMaturity + 1);

                // Create a transaction and set its timestamp to one that will be rejected from the block.
                Transaction tx = minerA.FullNode.WalletTransactionHandler().BuildTransaction(new TransactionBuildContext(network)
                {
                    Recipients = new List<Recipient>()
                    {
                        new Recipient
                        {
                            ScriptPubKey = addressUsed.ScriptPubKey,
                            Amount = Money.Coins(1m)
                        }
                    },
                    AccountReference = new WalletAccountReference(minerA.WalletName, "account 0"),
                    WalletPassword = minerA.WalletPassword,
                    Time = (uint)minerA.FullNode.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp()
                });
                minerA.AddToStratisMempool(tx);

                TestBase.WaitLoop(() => minerA.FullNode.MempoolManager().InfoAll().Count == 1);

                // Get our height right now.
                int currentHeight = minerA.FullNode.ChainIndexer.Height;

                // Start staking on the node.
                var minter = minerA.FullNode.NodeService<IPosMinting>();
                minter.Stake(new WalletSecret() { WalletName = minerA.WalletName, WalletPassword = minerA.WalletPassword });

                // Ensure we've staked a block.
                TestBase.WaitLoop(() => minerA.FullNode.ChainIndexer.Height > currentHeight);

                // Get the staked block.
                ChainedHeader header = minerA.FullNode.ChainIndexer.GetHeader(currentHeight + 1);
                Block block = minerA.FullNode.BlockStore().GetBlock(header.HashBlock);

                // The transaction should not be in it.
                Assert.DoesNotContain(block.Transactions, x => x.GetHash() == tx.GetHash());
            }
        }
    }
}