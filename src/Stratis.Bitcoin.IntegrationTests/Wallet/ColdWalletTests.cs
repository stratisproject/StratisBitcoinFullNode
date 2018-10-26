using System;
using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    /// <summary>
    /// Contains integration tests for the cold wallet feature.
    /// </summary>
    public class ColdWalletTests
    {
        private const string Password = "123456";
        private const string WalletName = "mywallet";
        private const string Passphrase = "passphrase";
        private const string Account = "account 0";
        private readonly Network network;

        public ColdWalletTests()
        {
            this.network = KnownNetworks.StratisRegTest;
        }

        /// <summary>
        /// Creates the transaction build context.
        /// </summary>
        /// <param name="network">The network that the context is for.</param>
        /// <param name="accountReference">The wallet account providing the funds.</param>
        /// <param name="password">the wallet password.</param>
        /// <param name="destinationScript">The destination script where we are sending the funds to.</param>
        /// <param name="amount">the amount of money to send.</param>
        /// <param name="feeType">The fee type.</param>
        /// <param name="minConfirmations">The minimum number of confirmations.</param>
        /// <returns>The transaction build context.</returns>
        private static TransactionBuildContext CreateContext(Network network, WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = accountReference,
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                WalletPassword = password,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList()
            };
        }

        /// <summary>
        /// Creates a cold staking node.
        /// </summary>
        /// <param name="nodeBuilder">The node builder that will be used to build the node.</param>
        /// <param name="network">The network that the node is being built for.</param>
        /// <param name="dataDir">The data directory used by the node.</param>
        /// <param name="coldStakeNode">Set to <c>false</c> to create a normal (non-cold-staking) node.</param>
        /// <returns>The created cold staking node.</returns>
        private CoreNode CreatePowPosMiningNode(NodeBuilder nodeBuilder, Network network, string dataDir, bool coldStakeNode)
        {
            var extraParams = new NodeConfigParameters { { "datadir", dataDir } };

            var buildAction = new Action<IFullNodeBuilder>(builder =>
            {
                builder.UseBlockStore()
                 .UsePosConsensus()
                 .UseMempool();

                if (coldStakeNode)
                {
                    builder.UseColdStakingWallet();
                }
                else
                {
                    builder.UseWallet();
                }

                builder
                 .AddPowPosMining()
                 .AddRPC()
                 .UseApi()
                 .MockIBD();
            });

            return nodeBuilder.CreateCustomNode(buildAction, KnownNetworks.StratisRegTest,
                ProtocolVersion.ALT_PROTOCOL_VERSION, configParameters: extraParams);
        }

        /// <summary>
        /// Tests whether a cold stake can be minted.
        /// </summary>
        /// <description>
        /// Sends funds from mined by a sending node to the hot wallet node. The hot wallet node creates
        /// the cold staking setup using a cold staking address obtained from the cold wallet node.
        /// Success is determined by whether the balance in the cold wallet increases.
        /// </description>
        [Fact]
        public void WalletCanMineWithColdWalletCoins()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = CreatePowPosMiningNode(builder, this.network, TestBase.CreateTestDir(this), coldStakeNode: false);
                CoreNode stratisHotStake = CreatePowPosMiningNode(builder, this.network, TestBase.CreateTestDir(this), coldStakeNode: true);
                CoreNode stratisColdStake = CreatePowPosMiningNode(builder, this.network, TestBase.CreateTestDir(this), coldStakeNode: true);

                stratisSender.WithWallet(Password, WalletName, Passphrase).Start();
                stratisHotStake.WithWallet(Password, WalletName, Passphrase).Start();
                stratisColdStake.WithWallet(Password, WalletName, Passphrase).Start();

                var senderWalletManager = stratisSender.FullNode.WalletManager() as ColdStakingManager;
                var coldWalletManager = stratisColdStake.FullNode.WalletManager() as ColdStakingManager;
                var hotWalletManager = stratisHotStake.FullNode.WalletManager() as ColdStakingManager;

                // Set up cold staking account on cold wallet.
                coldWalletManager.GetOrCreateColdStakingAccount(WalletName, true, Password);
                HdAddress coldWalletAddress = coldWalletManager.GetFirstUnusedColdStakingAddress(WalletName, true);
                coldWalletManager.UpdateKeysLookupLocked(new[] { coldWalletAddress });

                // Set up cold staking account on hot wallet.
                hotWalletManager.GetOrCreateColdStakingAccount(WalletName, false, Password);
                HdAddress hotWalletAddress = hotWalletManager.GetFirstUnusedColdStakingAddress(WalletName, false);
                hotWalletManager.UpdateKeysLookupLocked(new[] { hotWalletAddress });

                int maturity = (int)stratisSender.FullNode.Network.Consensus.CoinbaseMaturity;
                TestHelper.MineBlocks(stratisSender, maturity + 16, true, WalletName, Password, Account);

                int currentBestHeight = maturity + 16;

                // Wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));

                // The mining should add coins to the wallet
                long total = stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 98000060, total);

                int nConfirmations = 10;

                var walletAccountReference = new WalletAccountReference(WalletName, "account 0");
                long total2 = stratisSender.FullNode.WalletManager().GetSpendableTransactionsInAccount(walletAccountReference, nConfirmations).Sum(s => s.Transaction.Amount);

                // Sync all nodes
                stratisHotStake.CreateRPCClient().AddNode(stratisSender.Endpoint, true);
                stratisHotStake.CreateRPCClient().AddNode(stratisColdStake.Endpoint, true);
                stratisSender.CreateRPCClient().AddNode(stratisColdStake.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisHotStake, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisHotStake, stratisColdStake));

                // Send coins to hot wallet.
                Money amountToSend = Money.COIN * 98000059;
                HdAddress sendto = hotWalletManager.GetUnusedAddress(new WalletAccountReference(WalletName, Account));

                Transaction transaction1 = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(stratisSender.FullNode.Network, new WalletAccountReference(WalletName, Account), Password, sendto.ScriptPubKey, amountToSend, FeeType.Medium, nConfirmations));

                // Broadcast to the other node
                stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction1.ToHex()));

                // Wait for the transaction to arrive
                TestHelper.WaitLoop(() => stratisHotStake.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisHotStake.CreateRPCClient().GetRawTransaction(transaction1.GetHash(), false));
                TestHelper.WaitLoop(() => stratisHotStake.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

                long receivetotal = stratisHotStake.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(amountToSend, (Money)receivetotal);
                Assert.Null(stratisHotStake.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // Setup cold staking from the hot wallet.
                Money amountToSend2 = Money.COIN * 98000058;
                Transaction transaction2 = hotWalletManager.GetColdStakingSetupTransaction(stratisHotStake.FullNode.WalletTransactionHandler(),
                    coldWalletAddress.Address, hotWalletAddress.Address, WalletName, Account, Password, amountToSend2, new Money(0.02m, MoneyUnit.BTC));

                // Broadcast to the other node
                long mempoolSize = stratisColdStake.CreateRPCClient().GetRawMempool().Length;
                stratisHotStake.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));

                // Wait for the transaction to arrive
                TestHelper.WaitLoop(() => coldWalletManager.GetSpendableTransactionsInColdWallet(WalletName, true).Any());

                long receivetotal2 = coldWalletManager.GetSpendableTransactionsInColdWallet(WalletName, true).Sum(s => s.Transaction.Amount);
                Assert.Equal(amountToSend2, (Money)receivetotal2);
                Assert.Null(coldWalletManager.GetSpendableTransactionsInColdWallet(WalletName, true).First().Transaction.BlockHeight);

                // Allow coins to reach maturity
                TestHelper.MineBlocks(stratisSender, maturity, true, WalletName, Password, Account);

                // Start staking.
                var hotMiningFeature = stratisHotStake.FullNode.NodeFeature<MiningFeature>();
                hotMiningFeature.StartStaking(WalletName, Password);

                // Wait for new cold wallet transaction.
                TestHelper.WaitLoop(() =>
                {
                    // Keep mining to ensure that staking outputs reach maturity.
                    TestHelper.MineBlocks(stratisSender, 1, true, WalletName, Password, Account);
                    return coldWalletAddress.Transactions.Count > 1;
                });

                // Wait for money from staking.
                TestHelper.WaitLoop(() =>
                {
                    // Keep mining to ensure that staking outputs reach maturity.
                    TestHelper.MineBlocks(stratisSender, 1, true, WalletName, Password, Account);
                    return coldWalletManager.GetSpendableTransactionsInColdWallet(WalletName, true).Sum(s => s.Transaction.Amount) > receivetotal2;
                });
            }
        }
    }
}
