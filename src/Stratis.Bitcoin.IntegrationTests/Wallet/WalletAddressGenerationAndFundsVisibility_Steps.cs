using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
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
        
        private SharedSteps sharedSteps;
        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private long walletBalance;
        private long previousCoinBaseMaturity;
        private NodeBuilder nodeBuilder;
        private Network network;

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
            this.network = KnownNetworks.RegTest;
            this.sharedSteps = new SharedSteps();
        }

        protected override void AfterTest()
        {
            this.sendingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity = this.previousCoinBaseMaturity;
            this.receivingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity = this.previousCoinBaseMaturity;
            this.nodeBuilder.Dispose();
        }

        public Wallet_address_generation_and_funds_visibility(ITestOutputHelper output) : base(output)
        {
        }

        private void MineSpendableCoins()
        {
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
            this.sendingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode(this.network);
            this.sendingStratisBitcoinNode.Start();
            this.sendingStratisBitcoinNode.NotInIBD();
            Mnemonic sendingMnemonic = this.sendingStratisBitcoinNode.FullNode.WalletManager().CreateWallet(WalletPassword, SendingWalletName, WalletPassphrase);
            this.sendingStratisBitcoinNode.Mnemonic = sendingMnemonic;

            this.receivingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode(this.network);
            this.receivingStratisBitcoinNode.Start();
            this.receivingStratisBitcoinNode.NotInIBD();
            Mnemonic receivingMnemonic = this.receivingStratisBitcoinNode.FullNode.WalletManager().CreateWallet(WalletPassword, ReceivingWalletName, WalletPassphrase);
            this.receivingStratisBitcoinNode.Mnemonic = receivingMnemonic;

            TestHelper.ConnectAndSync(this.sendingStratisBitcoinNode, this.receivingStratisBitcoinNode);
            this.MineSpendableCoins();
        }

        private void a_gap_limit_of_21()
        {
            int customUnusedAddressBuffer = 21;
            var configParameters = new NodeConfigParameters { { "walletaddressbuffer", customUnusedAddressBuffer.ToString() } };

            this.sendingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode(this.network);
            this.sendingStratisBitcoinNode.Start();
            this.sendingStratisBitcoinNode.NotInIBD();
            Mnemonic sendingMnemonic = this.sendingStratisBitcoinNode.FullNode.WalletManager().CreateWallet(WalletPassword, SendingWalletName, WalletPassphrase);
            this.sendingStratisBitcoinNode.Mnemonic = sendingMnemonic;

            this.receivingStratisBitcoinNode = this.nodeBuilder.CreateStratisCustomPowNode(this.network, configParameters);
            this.receivingStratisBitcoinNode.Start();
            this.receivingStratisBitcoinNode.NotInIBD();
            Mnemonic receivingMnemonic = this.receivingStratisBitcoinNode.FullNode.WalletManager().CreateWallet(WalletPassword, ReceivingWalletName, WalletPassphrase);
            this.receivingStratisBitcoinNode.Mnemonic = receivingMnemonic;

            TestHelper.ConnectAndSync(this.sendingStratisBitcoinNode, this.receivingStratisBitcoinNode);
            this.MineSpendableCoins();
        }


        private void a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit()
        {
            ExtPubKey xPublicKey = this.GetExtendedPublicKey(this.receivingStratisBitcoinNode);
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

        private ExtPubKey GetExtendedPublicKey(CoreNode node)
        {
            ExtKey xPrivKey = node.Mnemonic.DeriveExtKey(WalletPassphrase);
            Key privateKey = xPrivKey.PrivateKey;
            ExtPubKey xPublicKey = HdOperations.GetExtendedPublicKey(privateKey, xPrivKey.ChainCode, (int)CoinType.Bitcoin, 0);
            return xPublicKey;
        }

        private void getting_wallet_balance()
        {
            this.sharedSteps.WaitForNodeToSync(this.sendingStratisBitcoinNode, this.receivingStratisBitcoinNode);

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
