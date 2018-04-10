using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;
using static Stratis.Bitcoin.Features.Miner.PosMinting;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public partial class ProofOfStakeMintCoinsSpecification
    {
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private const string PowMiner = "ProofOfWorkNode";
        private const string PosStaker = "ProofOfStakeNode";
        private SharedSteps sharedSteps;

        private HdAddress powSenderAddress;
        private Key powSenderPrivateKey;

        private HdAddress posReceiverAddress;
        private Key posReceiverPrivateKey;

        private const string PowWallet = "powwallet";
        private const string PowWalletAccount = "account 0";
        private const string PowWalletPassword = "password";

        private const string PosWallet = "poswallet";
        private const string PosWalletAccount = "account 0";
        private const string PosWalletPassword = "password";

        public ProofOfStakeMintCoinsSpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder();
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder.Dispose();
        }

        private void a_proof_of_work_node_with_wallet()
        {
            this.nodes = this.nodeGroupBuilder
                    .CreateStratisPowMiningNode(PowMiner)
                    .MineCoinsFast()
                    .Start()
                    .NotInIBD()
                    .WithWallet(PowWallet, PowWalletPassword)
                    .Build();

            this.powSenderAddress = this.nodes[PowMiner].FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(PowWallet, PowWalletAccount));
            var wallet = this.nodes[PowMiner].FullNode.WalletManager().GetWalletByName(PowWallet);
            this.powSenderPrivateKey = wallet.GetExtendedPrivateKeyForAddress(PowWalletPassword, this.powSenderAddress).PrivateKey;
        }

        private void it_mines_genesis_and_premine_blocks()
        {
            this.sharedSteps.MinePremineBlocks(this.nodes[PowMiner], PowWallet, PowWalletAccount, PowWalletPassword);
        }

        private void mine_coins_to_maturity()
        {
            this.nodes[PowMiner].GenerateStratisWithMiner(100);
            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[PowMiner]);
        }

        private void a_proof_of_stake_node_with_wallet()
        {
            this.nodeGroupBuilder
                    .CreateStratisPosNode(PosStaker)
                    .MineCoinsFast()
                    .Start()
                    .NotInIBD()
                    .WithWallet(PosWallet, PosWalletPassword)
                    .Build();
        }

        private void it_syncs_with_proof_work_node()
        {
            this.posReceiverAddress = this.nodes[PosStaker].FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(PosWallet, PosWalletAccount));
            var wallet = this.nodes[PosStaker].FullNode.WalletManager().GetWalletByName(PosWallet);
            this.posReceiverPrivateKey = wallet.GetExtendedPrivateKeyForAddress(PosWalletPassword, this.posReceiverAddress).PrivateKey;

            this.nodes[PosStaker].SetDummyMinerSecret(new BitcoinSecret(this.posReceiverPrivateKey, this.nodes[PosStaker].FullNode.Network));
            this.nodeGroupBuilder.WithConnections().Connect(PowMiner, PosStaker);
        }

        private void create_tx_to_send_million_coins_from_pow_wallet_to_pos_node_wallet()
        {
            var context = SharedSteps.CreateTransactionBuildContext(
                PowWallet,
                PowWalletAccount,
                PowWalletPassword,
                this.posReceiverAddress.ScriptPubKey,
                Money.COIN * 1000000,
                FeeType.Medium,
                101);

            context.OverrideFeeRate = new FeeRate(Money.Satoshis(20000));

            var sendTransaction = CreateSendTransaction();
            this.nodes[PowMiner].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(sendTransaction.ToHex()));
        }

        private Transaction CreateSendTransaction()
        {
            var unspent = this.nodes[PowMiner].FullNode.WalletManager().GetSpendableTransactionsInWallet(PowWallet);
            var coins = new List<Coin>();

            var blockTimestamp = unspent.OrderBy(u => u.Transaction.CreationTime).Select(ts => ts.Transaction.CreationTime).First();
            var transaction = new Transaction
            {
                Time = (uint)blockTimestamp.ToUnixTimeSeconds()
            };

            foreach (var item in unspent.OrderByDescending(a => a.Transaction.Amount))
            {
                coins.Add(new Coin(item.Transaction.Id, (uint)item.Transaction.Index, item.Transaction.Amount, item.Transaction.ScriptPubKey));
            }

            var coin = coins.First();
            var txIn = transaction.AddInput(new TxIn(coin.Outpoint, this.powSenderAddress.ScriptPubKey));
            transaction.AddOutput(new TxOut(new Money(9699999999995400), this.powSenderAddress.ScriptPubKey));
            transaction.AddOutput(new TxOut(new Money(100000000000000), this.posReceiverAddress.ScriptPubKey));
            transaction.Sign(this.powSenderPrivateKey, new[] { coin });

            return transaction;
        }

        private void pow_wallet_broadcasts_tx_of_million_coins_and_pos_wallet_receives()
        {
            // Wait for the coins to arrive
            TestHelper.WaitLoop(() => this.nodes[PosStaker].CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.nodes[PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(PosWallet).Any());
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[PosStaker]));

            // Ensure that the wallet on the proof-of-stake node reflects the coins sent (1 000 000)
            var received = this.nodes[PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(PosWallet);
            received.Sum(s => s.Transaction.Amount).Should().Be(Money.COIN * 1000000);
        }

        private void pos_node_mines_a_further_ten_blocks()
        {
            // Ensure coin maturity to stake the coins by mining
            // the coins on the proof stake node 10 times.
            // This is equal to the blocks incrementing their confirmation count by 10.
            this.nodes[PosStaker].GenerateStratisWithMiner(10);
        }

        private void pos_node_starts_staking()
        {
            var minter = this.nodes[PosStaker].FullNode.NodeService<IPosMinting>();
            minter.Stake(new WalletSecret() { WalletName = PosWallet, WalletPassword = PosWalletPassword });
        }

        private void pos_node_wallet_has_earned_coins_through_staking()
        {
            TestHelper.WaitLoop(() =>
            {
                var staked = this.nodes[PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(PosWallet).Sum(s => s.Transaction.Amount);
                return staked > Money.COIN * 1000000;
            });
        }
    }
}