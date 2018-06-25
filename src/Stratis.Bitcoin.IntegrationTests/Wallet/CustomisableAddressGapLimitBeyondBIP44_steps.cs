using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class Customisable_address_gap_limit_beyond_BIP44 : BddSpecification
    {
        private NodeGroupBuilder nodeGroupBuilder;
        private IDictionary<string, CoreNode> nodeGroup;
        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private SharedSteps sharedSteps;
        private long walletBalance;
        private const string SendingWalletName = "senderwallet";
        private const string ReceivingWalletName = "receivingwallet";
        private const string WalletPassword = "password";
        public const string AccountZero = "account 0";


        protected override void BeforeTest()
        {
            this.nodeGroupBuilder = new NodeGroupBuilder(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
            this.sharedSteps = new SharedSteps();
        }

        protected override void AfterTest()
        {
        }

        public Customisable_address_gap_limit_beyond_BIP44(ITestOutputHelper output) : base(output)
        {
        }

        private void a_default_gap_limit_of_20()
        {
            this.nodeGroup = this.nodeGroupBuilder
                .StratisPowNode("sending").Start().NotInIBD()
                .WithWallet(SendingWalletName, WalletPassword)
                .StratisPowNode("receiving").Start().NotInIBD()
                .WithWallet(ReceivingWalletName, WalletPassword)
                .WithConnections()
                .Connect("sending", "receiving")
                .AndNoMoreConnections()
                .Build();

            this.sendingStratisBitcoinNode = this.nodeGroup["sending"];
            this.receivingStratisBitcoinNode = this.nodeGroup["receiving"];

            this.sendingStratisBitcoinNode.FullNode
                .Network.Consensus.CoinbaseMaturity = 1;

            this.receivingStratisBitcoinNode.FullNode
                .Network.Consensus.CoinbaseMaturity = 1;


            var coinbaseMaturity = (int)this.sendingStratisBitcoinNode.FullNode
                .Network.Consensus.CoinbaseMaturity;

            this.sharedSteps.MineBlocks(coinbaseMaturity + 1, this.sendingStratisBitcoinNode, AccountZero, SendingWalletName, WalletPassword);
            
        }

        private void a_wallet_with_funds_at_index_21()
        {
            HdAddress recipientAddress = this.receivingStratisBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(ReceivingWalletName, AccountZero), 1000).Last();

            TransactionBuildContext transactionBuildContext = SharedSteps.CreateTransactionBuildContext(
                SendingWalletName,
                AccountZero,
                WalletPassword,
                new[] {
                        new Recipient {
                            Amount = Money.COIN * 1,
                            ScriptPubKey = recipientAddress.ScriptPubKey
                        }
                },
                FeeType.Medium, 0);

            var transaction = this.sendingStratisBitcoinNode.FullNode.WalletTransactionHandler()
                 .BuildTransaction(transactionBuildContext);

            this.sendingStratisBitcoinNode.FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(transaction.ToHex()));

            Transaction tx = this.sendingStratisBitcoinNode.FullNode.MempoolManager().GetTransaction(transaction.GetHash()).GetAwaiter().GetResult();
            tx.GetHash().Should().Be(transaction.GetHash());

            this.sharedSteps.MineBlocks(1, this.sendingStratisBitcoinNode, AccountZero, SendingWalletName, WalletPassword, 7480);
        }

        private void getting_wallet_balance()
        {
            this.sharedSteps.WaitForNodeToSync(this.nodeGroup.Values.ToArray());

            this.walletBalance = this.receivingStratisBitcoinNode.FullNode.WalletManager()
               .GetSpendableTransactionsInWallet(ReceivingWalletName)
               .Sum(s => s.Transaction.Amount);
        }

        private void the_balance_is_zero()
        {
            this.walletBalance.Should().Be(0);
        }
    }
}
