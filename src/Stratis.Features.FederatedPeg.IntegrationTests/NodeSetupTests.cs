using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Features.FederatedPeg.IntegrationTests.Utils;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.IntegrationTests
{
    public class NodeSetupTests
    {
        [Fact]
        public void MainChainFedNodesBuildAndSync()
        {
            using (var context = new SidechainTestContext())
            {
                context.StartMainNodes();
                context.ConnectMainChainNodes();
                context.EnableMainFedWallets();

                TestHelper.MineBlocks(context.MainUser, (int)context.MainChainNetwork.Consensus.CoinbaseMaturity + (int)context.MainChainNetwork.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.MainUser, context.FedMain1, context.FedMain2, context.FedMain3);
                Assert.True(context.MainUser.GetBalance() > context.MainChainNetwork.Consensus.PremineReward);
            }
        }

        [Fact(Skip = "Unstable")]
        public async Task SideChainFedNodesBuildAndSync()
        {
            using (var context = new SidechainTestContext())
            {
                context.StartSideNodes();
                context.ConnectSideChainNodes();
                context.EnableSideFedWallets();

                // Wait for node to reach premine height
                await context.FedSide1.MineBlocksAsync((int) context.SideChainNetwork.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide1, context.FedSide2, context.FedSide3);

                // Ensure that coinbase contains premine reward and it goes to the fed.
                Block block = context.SideUser.FullNode.ChainIndexer.GetHeader((int)context.SideChainNetwork.Consensus.PremineHeight).Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Equal(FederatedPegBlockDefinition.FederationWalletOutputs, coinbase.Outputs.Count);
                for (int i = 0; i < FederatedPegBlockDefinition.FederationWalletOutputs; i++)
                {
                    Assert.Equal(context.SideChainNetwork.Consensus.PremineReward / FederatedPegBlockDefinition.FederationWalletOutputs, coinbase.Outputs[i].Value);
                    Assert.Equal(context.scriptAndAddresses.payToMultiSig.PaymentScript, coinbase.Outputs[i].ScriptPubKey);
                }
            }
        }

        [Fact(Skip = "Inherently unreliable, but shows that the multiple UTXO approach allows parallel sending!")]
        public async Task ParallelWithdrawalsToSidechain()
        {
            using (var context = new SidechainTestContext())
            {
                // Set everything up
                context.StartAndConnectNodes();
                context.EnableSideFedWallets();
                context.EnableMainFedWallets();

                // Fund a main chain node
                TestHelper.MineBlocks(context.MainUser, (int)context.MainChainNetwork.Consensus.CoinbaseMaturity + (int)context.MainChainNetwork.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.MainUser, context.FedMain1);
                Assert.True(context.MainUser.GetBalance() > context.MainChainNetwork.Consensus.PremineReward);

                // Let sidechain progress to point where fed has the premine
                await context.FedSide1.MineBlocksAsync((int)context.SideChainNetwork.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide1);
                Block block = context.SideUser.FullNode.ChainIndexer.GetHeader((int)context.SideChainNetwork.Consensus.PremineHeight).Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Equal(FederatedPegBlockDefinition.FederationWalletOutputs, coinbase.Outputs.Count);

                // Send multiple deposits to sidechain
                decimal transferValueCoins = 10;
                string sidechainAddress = context.SideUser.GetUnusedAddress();

                // Lets send 3 deposits all exactly the same
                const int toSend = 3;
                for (int i = 0; i < toSend; i++)
                {
                    await context.DepositToSideChain(context.MainUser, transferValueCoins, sidechainAddress);
                }

                TestHelper.WaitLoop(() => context.FedMain1.CreateRPCClient().GetRawMempool().Length == toSend);

                // Mine enough blocks to make the deposits mature on the main chain
                TestHelper.MineBlocks(context.FedMain1, 6);

                // Wait until our sidechain nodes have all fully signed the transactions.
                ICrossChainTransferStore fedSideStore = context.FedSide1.FullNode.NodeService<ICrossChainTransferStore>();
                TestHelper.WaitLoop(() =>
                {
                    Dictionary<uint256, Transaction> fullySignedTransactions = fedSideStore.GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).GetAwaiter().GetResult();
                    return fullySignedTransactions.Count == toSend;
                });

                // Mine one more block on the main chain to trigger leader selection on sidechain
                TestHelper.MineBlocks(context.FedMain1, 1);

                // Wait for all the withdrawal transactions to reach the mempool on the sidechain
                TestHelper.WaitLoop(() =>
                {
                    int inMempool = context.FedSide1.CreateRPCClient().GetRawMempool().Length;
                    return inMempool == toSend;
                });

                await context.FedSide2.MineBlocksAsync(1);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide2);

                Block sideBlock = context.SideUser.FullNode.ChainIndexer.Tip.Block;
                Assert.Equal(toSend + 1, sideBlock.Transactions.Count);
            }
        }


        [Fact(Skip = TestingValues.SkipTests)]
        public void StartBothChainsWithWallets()
        {
            using (var context = new SidechainTestContext())
            {
                context.StartAndConnectNodes();

                context.EnableSideFedWallets();
                context.EnableMainFedWallets();
            }
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public async Task MainChain_To_SideChain_Transfer_And_Back()
        {
            using (var context = new SidechainTestContext())
            {
                // Set everything up
                context.StartAndConnectNodes();
                context.EnableSideFedWallets();
                context.EnableMainFedWallets();

                // Fund a main chain node
                TestHelper.MineBlocks(context.MainUser, (int)context.MainChainNetwork.Consensus.CoinbaseMaturity + (int)context.MainChainNetwork.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.MainUser, context.FedMain1);
                Assert.True(context.MainUser.GetBalance() > context.MainChainNetwork.Consensus.PremineReward);

                // Let sidechain progress to point where fed has the premine
                TestHelper.WaitLoop(() => context.SideUser.FullNode.ChainIndexer.Height >= context.SideUser.FullNode.Network.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide1);
                Block block = context.SideUser.FullNode.ChainIndexer.GetHeader((int)context.SideChainNetwork.Consensus.PremineHeight).Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Single(coinbase.Outputs);
                Assert.Equal(context.SideChainNetwork.Consensus.PremineReward, coinbase.Outputs[0].Value);
                Assert.Equal(context.scriptAndAddresses.payToMultiSig.PaymentScript, coinbase.Outputs[0].ScriptPubKey);

                // Send to sidechain
                decimal transferValueCoins = 25;
                var transferValue = new Money(transferValueCoins, MoneyUnit.BTC);
                string sidechainAddress = context.SideUser.GetUnusedAddress();
                await context.DepositToSideChain(context.MainUser, transferValueCoins, sidechainAddress);
                TestHelper.WaitLoop(() => context.FedMain1.CreateRPCClient().GetRawMempool().Length == 1);
                TestHelper.MineBlocks(context.FedMain1, 15);

                var source = new CancellationTokenSource(15_000);
                TestHelper.WaitLoop(() => context.SideUser.GetBalance() == transferValue, cancellationToken: source.Token);

                // Sidechain user has balance - transfer complete.
                Assert.Equal(transferValue, context.SideUser.GetBalance());

                await Task.Delay(5_000).ConfigureAwait(false);

                // Send funds back to the main chain
                string mainchainAddress = context.MainUser.GetUnusedAddress();
                Money currentMainUserBalance = context.MainUser.GetBalance();

                await context.WithdrawToMainChain(context.SideUser, 24, mainchainAddress);
                int currentSideHeight = context.SideUser.FullNode.ChainIndexer.Tip.Height;
                // Mine just enough to get past min deposit and allow time for fed to work
                TestHelper.WaitLoop(() => context.SideUser.FullNode.ChainIndexer.Height >= currentSideHeight + 7);

                // Should unlock funds back on the main chain
                TestHelper.WaitLoop(() => context.FedMain1.CreateRPCClient().GetRawMempool().Length == 1);
                TestHelper.MineBlocks(context.FedMain1, 1);
                Assert.Equal(currentMainUserBalance + new Money(24, MoneyUnit.BTC), context.MainUser.GetBalance());
            }
        }
    }
}
