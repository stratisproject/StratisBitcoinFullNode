using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfWorkSpendingSpecification
    {
        private const string SendingWalletName = "sending wallet";
        private const string ReceivingWalletName = "receiving wallet";
        private NodeBuilder nodeBuilder;
        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;
        private Transaction lastTransaction;
        private int totalMinedBlocks;

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void a_sending_and_receiving_stratis_bitcoin_node_and_wallet()
        {
            this.sendingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode();
            this.receivingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode();

            this.nodeBuilder.StartAll();
            this.sendingStratisBitcoinNode.NotInIBD();
            this.receivingStratisBitcoinNode.NotInIBD();

            this.sendingStratisBitcoinNode.CreateRPCClient().AddNode(this.receivingStratisBitcoinNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.receivingStratisBitcoinNode, this.sendingStratisBitcoinNode));

            this.sendingStratisBitcoinNode.FullNode.WalletManager().CreateWallet("123456", SendingWalletName);
            this.receivingStratisBitcoinNode.FullNode.WalletManager().CreateWallet("123456", ReceivingWalletName);

            this.coinbaseMaturity = (int)this.sendingStratisBitcoinNode.FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
        }

        private void a_block_is_mined_creating_spendable_coins()
        {
            this.MineBlocks(1, this.sendingStratisBitcoinNode);
        }

        private void more_blocks_mined_to_just_before_maturity_of_original_block()
        {
            this.MineBlocks(this.coinbaseMaturity - 1, this.sendingStratisBitcoinNode);
        }

        private void more_blocks_mined_to_just_after_maturity_of_original_block()
        {
            this.MineBlocks(this.coinbaseMaturity, this.sendingStratisBitcoinNode);
        }

        private void MineBlocks(int blockCount, CoreNode node)
        {
            var address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(SendingWalletName, "account 0"));
            var wallet = node.FullNode.WalletManager().GetWalletByName(SendingWalletName);
            var extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress("123456", address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            node.GenerateStratisWithMiner(blockCount);
                        this.totalMinedBlocks = this.totalMinedBlocks + blockCount;

            this.sendingStratisBitcoinNode.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(SendingWalletName)
                .Sum(s => s.Transaction.Amount)
                .Should().Be(Money.COIN * this.totalMinedBlocks * 50);

            //wait for block store to sync(or catch-up)
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(node));
        }

        private void spending_the_coins_from_original_block()
        {
            var sendtoAddress = this.receivingStratisBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(ReceivingWalletName, "account 0"), 2).ElementAt(1);

            try
            {
                this.lastTransaction = this.sendingStratisBitcoinNode.FullNode.WalletTransactionHandler().BuildTransaction(
                    CreateTransactionBuildContext(new WalletAccountReference(SendingWalletName, "account 0"), "123456", sendtoAddress.ScriptPubKey,
                        Money.COIN * 1, FeeType.Medium, 101));

                this.sendingStratisBitcoinNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.lastTransaction.ToHex()));
            }
            catch (Exception exception)
            {
                this.caughtException = exception;
            }
        }

        private void the_transaction_is_rejected_from_the_mempool()
        {
            this.caughtException.Should().BeOfType<WalletException>();

            var walletException = (WalletException)this.caughtException;
            walletException.Message.Should().Be("No spendable transactions found.");

            // reset for any future checks in same test
            this.caughtException = null;
        }

        private void the_transaction_is_put_in_the_mempool()
        {
            var tx = this.sendingStratisBitcoinNode.FullNode.MempoolManager().GetTransaction(this.lastTransaction.GetHash()).GetAwaiter().GetResult();
            tx.GetHash().Should().Be(this.lastTransaction.GetHash());
            this.caughtException.Should().BeNull();
        }

        public static TransactionBuildContext CreateTransactionBuildContext(WalletAccountReference accountReference, string password, Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }
    }
}
