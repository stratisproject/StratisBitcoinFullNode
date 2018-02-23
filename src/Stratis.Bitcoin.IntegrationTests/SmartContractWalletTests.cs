using System;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SmartContractWalletTests : IDisposable
    {
        private bool initialBlockSignature;

        public SmartContractWalletTests()
        {
            this.initialBlockSignature = Block.BlockSignature;
            Block.BlockSignature = false;
        }

        public void Dispose()
        {
            Block.BlockSignature = this.initialBlockSignature;
        }

        [Fact]
        public void SendAndReceiveCorrectly()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var scSender = builder.CreateSmartContractNode();
                var scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();
                scSender.NotInIBD();
                scReceiver.NotInIBD();

                var mnemonic1 = scSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                var mnemonic2 = scReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");

            }

            //    var stratisSender = builder.CreateStratisPowNode();
            //    var stratisReceiver = builder.CreateStratisPowNode();

            //    builder.StartAll();
            //    stratisSender.NotInIBD();
            //    stratisReceiver.NotInIBD();

            //    // get a key from the wallet
            //    var mnemonic1 = stratisSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
            //    var mnemonic2 = stratisReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");
            //    Assert.Equal(12, mnemonic1.Words.Length);
            //    Assert.Equal(12, mnemonic2.Words.Length);
            //    var addr = stratisSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
            //    var wallet = stratisSender.FullNode.WalletManager().GetWalletByName("mywallet");
            //    var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

            //    stratisSender.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));
            //    var maturity = (int)stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
            //    stratisSender.GenerateStratis(maturity + 5);
            //    // wait for block repo for block sync to work

            //    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));

            //    // the mining should add coins to the wallet
            //    var total = stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
            //    Assert.Equal(Money.COIN * 105 * 50, total);

            //    // sync both nodes
            //    stratisSender.CreateRPCClient().AddNode(stratisReceiver.Endpoint, true);
            //    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));

            //    // send coins to the receiver
            //    var sendto = stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
            //    var trx = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(
            //        new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

            //    // broadcast to the other node
            //    stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));

            //    // wait for the trx to arrive
            //    TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
            //    TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

            //    var receivetotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
            //    Assert.Equal(Money.COIN * 100, receivetotal);
            //    Assert.Null(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

            //    // generate two new blocks do the trx is confirmed
            //    stratisSender.GenerateStratis(1, new List<Transaction>(new[] { trx.Clone() }));
            //    stratisSender.GenerateStratis(1);

            //    // wait for block repo for block sync to work
            //    TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));
            //    TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));

            //    TestHelper.WaitLoop(() => maturity + 6 == stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

            //}
        }
    }
}
