using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Connection;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class WalletTests
    {
        [Fact]
        public void WalletCanReceiveCorrectly()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
				var stratisSender = builder.CreateStratisNode();
				var stratisReceiver = builder.CreateStratisNode();

				builder.StartAll();
				stratisSender.NotInIBD();
				stratisReceiver.NotInIBD();

				// get a key from the wallet
	            var mnemonic1 = stratisSender.FullNode.WalletManager.CreateWallet("123456", "mywallet");
	            Assert.Equal(12, mnemonic1.Words.Length);

	            var mnemonic2 = stratisReceiver.FullNode.WalletManager.CreateWallet("123456", "mywallet");
	            Assert.Equal(12, mnemonic2.Words.Length);

				var addr = stratisSender.FullNode.WalletManager.GetUnusedAddress("mywallet", "account 0");
	            var key = stratisSender.FullNode.WalletManager.GetKeyForAddress("123456", addr).PrivateKey;

	            stratisSender.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));
	            stratisSender.GenerateStratis(105); // coinbase maturity = 10
				// wait for block repo for block sync to work

				Class1.Eventually(() => stratisSender.FullNode.ConsensusLoop.Tip.HashBlock == stratisSender.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisSender.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock == stratisSender.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisSender.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisSender.FullNode.Chain.Tip.HashBlock);

				// the mining should add coins to the wallet
	            var total = stratisSender.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
				Assert.Equal(Money.COIN * 105 * 50, total);

				// sync both nodes
				stratisSender.CreateRPCClient().AddNode(stratisReceiver.Endpoint, true);
				Class1.Eventually(() => stratisSender.CreateRPCClient().GetBestBlockHash() == stratisReceiver.CreateRPCClient().GetBestBlockHash());

				// send coins to the receiver
				var sendto = stratisReceiver.FullNode.WalletManager.GetUnusedAddress("mywallet", "account 0");
	            var trx = stratisSender.FullNode.WalletManager.BuildTransaction("mywallet", "account 0", "123456", sendto.Address, Money.COIN * 100, string.Empty, 101);

				// broadcast to the other node
	            stratisSender.FullNode.WalletManager.SendTransaction(trx.hex);

				// wait for the trx to arrive
	            Class1.Eventually(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
	            Class1.Eventually(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Any());

				var receivetotal = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
	            Assert.Equal(Money.COIN * 100, receivetotal);
	            Assert.Null(stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).First().BlockHeight);

				// generate two new blocks do the trx is confirmed
	            stratisSender.GenerateStratis(2, new List<Transaction>(new[] {new Transaction(trx.hex)}));

				// wait for block repo for block sync to work
				Class1.Eventually(() => stratisSender.FullNode.Chain.Tip.HashBlock == stratisSender.FullNode.ConsensusLoop.Tip.HashBlock);
				Class1.Eventually(() => stratisSender.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisSender.CreateRPCClient().GetBestBlockHash()).Result != null);
				Class1.Eventually(() => stratisSender.CreateRPCClient().GetBestBlockHash() == stratisReceiver.CreateRPCClient().GetBestBlockHash());

	            Class1.Eventually(() => 106 == stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).First().BlockHeight);
            }

        }
    }
}
