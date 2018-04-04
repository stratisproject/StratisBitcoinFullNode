﻿using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;
using static Stratis.Bitcoin.Features.Miner.PosMinting;

namespace Stratis.Bitcoin.IntegrationTests
{
    public sealed class PoSMintingTests
    {
        [Fact]
        public void ProofOfStake_MintNewCoinsViaStaking()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var nodeGenerateCoins = builder.CreateStratisPowNode();
                nodeGenerateCoins.StartAsync().GetAwaiter().GetResult();
                nodeGenerateCoins.NotInIBD();

                // Set up wallet to generate coins.
                var powWallet = CreateWallet(nodeGenerateCoins, "password", "mywallet");

                // Generate 2 PoW blocks, the second block will give us the premine of 98 million coins.
                nodeGenerateCoins.SetDummyMinerSecret(new BitcoinSecret(powWallet.PrivateKey, nodeGenerateCoins.FullNode.Network));
                nodeGenerateCoins.GenerateStratisWithMiner(2);

                // Wait for the node to mine the 2 blocks.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(nodeGenerateCoins));

                // Wallet balance should reflect 98 000 004 STRAT (Proof of work reward plus pre-mine)
                var spendable = nodeGenerateCoins.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet");
                Assert.Equal(Money.COIN * 98000004, spendable.Sum(s => s.Transaction.Amount));

                // Ensure the block maturity for pos consenus by mining the blocks 100 times.
                nodeGenerateCoins.GenerateStratisWithMiner(100);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(nodeGenerateCoins));

                // Create a Proof-Of-Stake node
                var nodeProofOfStake = builder.CreateStratisPosNode();
                nodeProofOfStake.StartAsync().GetAwaiter().GetResult();
                nodeProofOfStake.NotInIBD();

                // Set up a wallet on the proof of stake node to received the coins.
                var posWallet = CreateWallet(nodeProofOfStake, "password", "mywallet");

                // Wait for the PoS node to sync the 102 blocks from the proof of work node
                nodeProofOfStake.SetDummyMinerSecret(new BitcoinSecret(posWallet.PrivateKey, nodeProofOfStake.FullNode.Network));
                nodeProofOfStake.CreateRPCClient().AddNode(nodeGenerateCoins.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(nodeGenerateCoins, nodeProofOfStake));

                // Send 1 000 000 coins to the Proof-Of-Stake wallet
                var walletContext = CreateContext(
                    new WalletAccountReference("mywallet", "account 0"),
                    "password",
                    posWallet.Address.ScriptPubKey,
                    Money.COIN * 1000000,
                    FeeType.Medium,
                    101);
                walletContext.OverrideFeeRate = new FeeRate(Money.Satoshis(20000));

                // Broadcast the transaction from the PoW node
                var sendTransaction = CreateSendTransaction(nodeGenerateCoins, powWallet, powWallet.Address.ScriptPubKey, posWallet.Address.ScriptPubKey);
                nodeGenerateCoins.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(sendTransaction.ToHex()));

                // Wait for the coins to arrive
                TestHelper.WaitLoop(() => nodeProofOfStake.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.WaitLoop(() => nodeProofOfStake.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(nodeProofOfStake));

                // Ensure that the wallet on the proof-of-stake node
                // reflects the coins sent (1 000 000)
                var received = nodeProofOfStake.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet");
                Assert.Equal(Money.COIN * 1000000, received.Sum(s => s.Transaction.Amount));

                // Ensure coin maturity to stake the coins by mining
                // the coins on the proof stake node 10 times.
                // This is equal to the blocks incrementing their confirmation
                // count by 10.
                nodeProofOfStake.GenerateStratisWithMiner(10);

                // Start staking on the proof-of-stake node
                var minter = nodeProofOfStake.FullNode.NodeService<IPosMinting>();
                minter.Stake(new WalletSecret() { WalletName = "mywallet", WalletPassword = "password" });

                // Wait until some coins have been rewarded to the staking wallet.
                TestHelper.WaitLoop(() =>
                {
                    var staked = nodeProofOfStake.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                    return staked > Money.COIN * 1000000;
                });

                // Shutdown
                minter.StopStake();
            }
        }

        private Transaction CreateSendTransaction(CoreNode node, TestWallet senderWallet, Script sender, Script receiver)
        {
            var unspent = node.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet");
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
            var txIn = transaction.AddInput(new TxIn(coin.Outpoint, sender));
            transaction.AddOutput(new TxOut(new Money(9699999999995400), sender));
            transaction.AddOutput(new TxOut(new Money(100000000000000), receiver));
            transaction.Sign(senderWallet.PrivateKey, false);

            return transaction;
        }

        private TestWallet CreateWallet(CoreNode node, string walletPassword, string walletName)
        {
            node.FullNode.WalletManager().CreateWallet(walletPassword, walletName);
            var unusedAddress = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, "account 0"));
            var wallet = node.FullNode.WalletManager().GetWalletByName(walletName);
            var privateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, unusedAddress).PrivateKey;
            return new TestWallet() { Address = unusedAddress, PrivateKey = privateKey };
        }

        private class TestWallet
        {
            public HdAddress Address { get; set; }
            public Key PrivateKey { get; set; }
        }

        private TransactionBuildContext CreateContext(WalletAccountReference accountReference, string password, Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference, new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }
    }
}