using System;
using System.Threading;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
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

        private void TestSegwit_MinedOnStratisNode_ActivatedOn_CoreNode()
        {
            // TODO: mine segwit onh a stratis node on the bitcoin network
            // write a tests that mines segwit blocks on the stratis node
            // and signals them to a core not, then segwit will get activated on core
        }
    }
}
