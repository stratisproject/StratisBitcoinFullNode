using System;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerTests
    {
        private readonly Network network;

        private const string walletAccount = "account 0";
        private const string walletName = "mywallet";
        private const string walletPassword = "123456";

        public ConsensusManagerTests()
        {
            this.network = new StratisRegTestMaxReorg();
            this.network.Consensus.Options = new ConsensusOptionsTest();
        }

        private class ConsensusOptionsTest : PosConsensusOptions
        {
            public override int GetStakeMinConfirmations(int height, Network network)
            {
                return height < 10 ? 5 : 10;
            }
        }

        private class StratisRegTestMaxReorg : StratisRegTest
        {
            public StratisRegTestMaxReorg()
            {
                this.Consensus = new NBitcoin.Consensus(
                    consensusFactory: this.Consensus.ConsensusFactory,
                    consensusOptions: new ConsensusOptionsTest(),
                    coinType: 105,
                    hashGenesisBlock: this.GenesisHash,
                    subsidyHalvingInterval: 210000,
                    majorityEnforceBlockUpgrade: 750,
                    majorityRejectBlockOutdated: 950,
                    majorityWindow: 1000,
                    buriedDeployments: this.Consensus.BuriedDeployments,
                    bip9Deployments: this.Consensus.BIP9Deployments,
                    bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                    ruleChangeActivationThreshold: 1916, // 95% of 2016
                    minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                    maxReorgLength: 20,
                    defaultAssumeValid: null, // turn off assumevalid for regtest.
                    maxMoney: long.MaxValue,
                    coinbaseMaturity: 10,
                    premineHeight: 2,
                    premineReward: Money.Coins(98000000),
                    proofOfWorkReward: Money.Coins(4),
                    powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                    powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                    powAllowMinDifficultyBlocks: true,
                    powNoRetargeting: true,
                    powLimit: this.Consensus.PowLimit,
                    minimumChainWork: null,
                    isProofOfStake: true,
                    lastPowBlock: 12500,
                    proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                    proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                    proofOfStakeReward: Money.COIN
                );
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Scenario_1()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.network);
                var minerB = builder.CreateStratisPosNode(this.network);
                var syncer = builder.CreateStratisPosNode(this.network);

                builder.StartAll();

                minerA.NotInIBD().WithWallet();
                minerB.NotInIBD().WithWallet();
                syncer.NotInIBD();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 10);

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
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 10);

                // Miner B continues to mine to height 14 whilst disconnected.
                TestHelper.MineBlocks(minerB, walletName, walletPassword, walletAccount, 4);

                // Syncer now connects to both miners.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);

                // Ensure that Syncer has synced with Miner A which is the node with the best tip.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Miner A needs to mine 20 more blocks so that Miner B can be disconnected from syncer.
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 20);

                // Ensure that Syncer is not connected to miner B any longer.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));
            }
        }
    }
}