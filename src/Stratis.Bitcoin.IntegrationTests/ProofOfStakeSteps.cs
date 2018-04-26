using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using static Stratis.Bitcoin.Features.Miner.PosMinting;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ProofOfStakeSteps
    {
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private SharedSteps sharedSteps;

        private HdAddress powSenderAddress;
        private Key powSenderPrivateKey;

        private HdAddress posReceiverAddress;
        private Key posReceiverPrivateKey;

        private const string PowMiner = "ProofOfWorkNode";
        private const string PosStaker = "ProofOfStakeNode";

        private const string PowWallet = "powwallet";
        private const string PowWalletPassword = "password";

        private const string PosWallet = "poswallet";
        private const string PosWalletPassword = "password";

        private const string WalletAccount = "account 0";        

        public ProofOfStakeSteps(IDictionary<string, CoreNode> nodes)
        {
            this.nodes = nodes;
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder();
        }

        public void GenerateCoins()
        {
            ProofOfWorkNodeWithWallet();
            MineGenesisAndPremineBlocks();
            MineCoinsToMaturity();
            ProofOfStakeNodeWithWallet();
            SyncWithProofWorkNode();
            SendMillionCoinsFromPowWalletToPosWallet();
            PowWalletBroadcastsTransactionToPosWallet();
            PosNodeMinesToEnsureStaking();
            PosNodeStartsStaking();
            PosNodeWalletHasEarnedCoinsThroughStaking();
        }

        public Key GetProofOfStakeWalletKey()
        {
            var address = this.nodes[PosStaker].FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(PosWallet, WalletAccount));
            var wallet = this.nodes[PosStaker].FullNode.WalletManager().GetWalletByName(PosWallet);
            
            return wallet.GetExtendedPrivateKeyForAddress(PosWalletPassword, address).PrivateKey; ;
        }

        public void ProofOfWorkNodeWithWallet()
        {
            this.nodes = this.nodeGroupBuilder
                    .CreateStratisPowMiningNode(PowMiner)
                    .Start()
                    .NotInIBD()
                    .WithWallet(PowWallet, PowWalletPassword)
                    .Build();

            this.powSenderAddress = this.nodes[PowMiner].FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(PowWallet, WalletAccount));
            var wallet = this.nodes[PowMiner].FullNode.WalletManager().GetWalletByName(PowWallet);
            this.powSenderPrivateKey = wallet.GetExtendedPrivateKeyForAddress(PowWalletPassword, this.powSenderAddress).PrivateKey;
        }

        public void MineGenesisAndPremineBlocks()
        {
            this.sharedSteps.MinePremineBlocks(this.nodes[PowMiner], PowWallet, WalletAccount, PowWalletPassword);
        }

        public void MineCoinsToMaturity()
        {
            this.nodes[PowMiner].GenerateStratisWithMiner(100);
            this.sharedSteps.WaitForNodeToSync(this.nodes[PowMiner]);
        }

        public void ProofOfStakeNodeWithWallet()
        {
            this.nodeGroupBuilder
                    .CreateStratisPosNode(PosStaker)
                    .Start()
                    .NotInIBD()
                    .WithWallet(PosWallet, PosWalletPassword)
                    .Build();
        }

        public void SyncWithProofWorkNode()
        {
            this.posReceiverAddress = this.nodes[PosStaker].FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(PosWallet, WalletAccount));
            var wallet = this.nodes[PosStaker].FullNode.WalletManager().GetWalletByName(PosWallet);
            this.posReceiverPrivateKey = wallet.GetExtendedPrivateKeyForAddress(PosWalletPassword, this.posReceiverAddress).PrivateKey;

            this.nodes[PosStaker].SetDummyMinerSecret(new BitcoinSecret(this.posReceiverPrivateKey, this.nodes[PosStaker].FullNode.Network));
            this.nodeGroupBuilder.WithConnections().Connect(PowMiner, PosStaker);
        }

        public void SendMillionCoinsFromPowWalletToPosWallet()
        {
            var context = SharedSteps.CreateTransactionBuildContext(
                PowWallet,
                WalletAccount,
                PowWalletPassword,
                new List<Recipient>() { new Recipient { Amount = Money.COIN * 1000000, ScriptPubKey = this.posReceiverAddress.ScriptPubKey } },
                FeeType.Medium,
                101);

            context.OverrideFeeRate = new FeeRate(Money.Satoshis(20000));

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

            this.nodes[PowMiner].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
        }

        public void PowWalletBroadcastsTransactionToPosWallet()
        {
            TestHelper.WaitLoop(() => this.nodes[PosStaker].CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.nodes[PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(PosWallet).Any());

            this.sharedSteps.WaitForNodeToSync(this.nodes[PosStaker]);

            var received = this.nodes[PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(PosWallet);
            received.Sum(s => s.Transaction.Amount).Should().Be(Money.COIN * 1000000);
        }

        public void PosNodeMinesToEnsureStaking()
        {
            this.nodes[PosStaker].GenerateStratisWithMiner(Convert.ToInt32(this.nodes[PosStaker].FullNode.Network.Consensus.Option<PosConsensusOptions>().CoinbaseMaturity));
        }

        public void PosNodeStartsStaking()
        {
            var minter = this.nodes[PosStaker].FullNode.NodeService<IPosMinting>();
            minter.Stake(new WalletSecret() { WalletName = PosWallet, WalletPassword = PosWalletPassword });
        }

        public void PosNodeWalletHasEarnedCoinsThroughStaking()
        {
            TestHelper.WaitLoop(() =>
            {
                var staked = this.nodes[PosStaker].FullNode.WalletManager().GetSpendableTransactionsInWallet(PosWallet).Sum(s => s.Transaction.Amount);
                return staked >= Money.COIN * 1000000;
            });
        }
    }
}
