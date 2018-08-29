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
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class Wallet_address_generation_and_funds_visibility : BddSpecification
    {
        private const string SendingWalletName = "senderwallet";
        private const string ReceivingWalletName = "receivingwallet";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "passphrase";
        public const string AccountZero = "account 0";
        private const string ReceivingNodeName = "receiving";
        private const string SendingNodeName = "sending";

        private SharedSteps sharedSteps;
        private NodeGroupBuilder nodeGroupBuilder;
        private IDictionary<string, CoreNode> nodeGroup;
        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private long walletBalance;
        private long previousCoinBaseMaturity;

        protected override void BeforeTest()
        {
            this.nodeGroupBuilder = new NodeGroupBuilder(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName), KnownNetworks.RegTest);
            this.sharedSteps = new SharedSteps();
        }

        protected override void AfterTest()
        {
            this.sendingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity = this.previousCoinBaseMaturity;
            this.receivingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity = this.previousCoinBaseMaturity;
            this.nodeGroupBuilder.Dispose();
        }

        public Wallet_address_generation_and_funds_visibility(ITestOutputHelper output) : base(output)
        {
        }

        private void MineSpendableCoins()
        {
            this.sendingStratisBitcoinNode = this.nodeGroup[SendingNodeName];
            this.receivingStratisBitcoinNode = this.nodeGroup[ReceivingNodeName];

            this.sendingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity.Should()
                .Be(this.receivingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity);
            this.previousCoinBaseMaturity = this.sendingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity;

            var coinbaseMaturity = 1;

            this.sendingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity = coinbaseMaturity;
            this.receivingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity = coinbaseMaturity;

            this.sharedSteps.MineBlocks(coinbaseMaturity + 1, this.sendingStratisBitcoinNode, AccountZero, SendingWalletName,
                WalletPassword);
        }

        private void a_default_gap_limit_of_20()
        {
            this.nodeGroup = this.nodeGroupBuilder
                .StratisPowNode(SendingNodeName).Start().NotInIBD()
                .WithWallet(SendingWalletName, WalletPassword, WalletPassphrase)
                .StratisPowNode(ReceivingNodeName).Start().NotInIBD()
                .WithWallet(ReceivingWalletName, WalletPassword, WalletPassphrase)
                .WithConnections()
                .Connect(SendingNodeName, ReceivingNodeName)
                .AndNoMoreConnections()
                .Build();

            MineSpendableCoins();
        }

        private void a_gap_limit_of_21()
        {
            int customUnusedAddressBuffer = 21;
            var configParameters =
                new NodeConfigParameters { { "walletaddressbuffer", customUnusedAddressBuffer.ToString() } };
            this.nodeGroup = this.nodeGroupBuilder
                .StratisPowNode(SendingNodeName).Start().NotInIBD()
                .WithWallet(SendingWalletName, WalletPassword, WalletPassphrase)
                .StratisCustomPowNode(ReceivingNodeName, configParameters).Start()
                .NotInIBD()
                .WithWallet(ReceivingWalletName, WalletPassword, WalletPassphrase)
                .WithConnections()
                .Connect(SendingNodeName, ReceivingNodeName)
                .AndNoMoreConnections()
                .Build();

            MineSpendableCoins();
        }


        private void a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit()
        {
            ExtPubKey xPublicKey = this.GetExtendedPublicKey(ReceivingNodeName);
            var recipientAddressBeyondGapLimit = xPublicKey.Derive(new KeyPath("0/20")).PubKey.GetAddress(KnownNetworks.RegTest);

            TransactionBuildContext transactionBuildContext = SharedSteps.CreateTransactionBuildContext(
                this.sendingStratisBitcoinNode.FullNode.Network,
                SendingWalletName,
                AccountZero,
                WalletPassword,
                new[] {
                        new Recipient {
                            Amount = Money.COIN * 1,
                            ScriptPubKey = recipientAddressBeyondGapLimit.ScriptPubKey
                        }
                },
                FeeType.Medium, 0);

            var transaction = this.sendingStratisBitcoinNode.FullNode.WalletTransactionHandler()
                 .BuildTransaction(transactionBuildContext);

            this.sendingStratisBitcoinNode.FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(transaction.ToHex()));

            this.sharedSteps.MineBlocks(1, this.sendingStratisBitcoinNode, AccountZero, SendingWalletName, WalletPassword);
        }

        private ExtPubKey GetExtendedPublicKey(string nodeName)
        {
            ExtKey xPrivKey = this.nodeGroupBuilder.NodeMnemonics[nodeName].DeriveExtKey(WalletPassphrase);
            Key privateKey = xPrivKey.PrivateKey;
            ExtPubKey xPublicKey = HdOperations.GetExtendedPublicKey(privateKey, xPrivKey.ChainCode, (int)CoinType.Bitcoin, 0);
            return xPublicKey;
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

        private void _21_new_addresses_are_requested()
        {
            this.receivingStratisBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(ReceivingWalletName, AccountZero), 21);
        }

        private void the_balance_is_NOT_zero()
        {
            this.walletBalance.Should().Be(1 * Money.COIN);
        }
    }
}
