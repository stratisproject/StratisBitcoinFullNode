using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SmartContractWalletTests : IDisposable
    {
        private bool initialBlockSignature;

        public SmartContractWalletTests()
        {
            this.initialBlockSignature = NBitcoin.Block.BlockSignature;
            NBitcoin.Block.BlockSignature = false;
        }

        public void Dispose()
        {
            NBitcoin.Block.BlockSignature = this.initialBlockSignature;
        }


        /// <summary>
        /// This is the same test in WalletTests.cs, just using all of the smart contract classes
        /// </summary>
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
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = scSender.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                scSender.GenerateSmartContractStratis(maturity + 5);
                // wait for block repo for block sync to work

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // the mining should add coins to the wallet
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 105 * 50, total);

                // sync both nodes
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // send coins to the receiver
                var sendto = scReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var txBuildContext = new TransactionBuildContext(new WalletAccountReference("mywallet", "account 0"),
                        new[] { new Recipient { Amount = Money.COIN * 100, ScriptPubKey = sendto.ScriptPubKey } }.ToList(), "123456")
                        {
                            MinConfirmations = 101,
                            FeeType = FeeType.Medium
                        };

                var trx = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);

                // broadcast to the other node
                scSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => scReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.WaitLoop(() => scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

                var receivetotal = scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // generate two new blocks do the trx is confirmed
                scSender.GenerateSmartContractStratis(1, new List<Transaction>(new[] { trx.Clone() }));
                scSender.GenerateSmartContractStratis(1);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                TestHelper.WaitLoop(() => maturity + 6 == scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);
            }
        }

        [Fact]
        public void SendAndReceiveSmartContractTransactions()
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
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = scSender.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                scSender.GenerateSmartContractStratisWithMiner(maturity + 5);
                // wait for block repo for block sync to work

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // the mining should add coins to the wallet
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 105 * 50, total);

                // Create a contract
                ulong gasPrice = 1;
                uint vmVersion = 1;
                Gas gasLimit = (Gas)1000;
                var gasBudget = gasPrice * gasLimit;
                var contractCarrier = SmartContractCarrier.CreateContract(vmVersion, GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/TransferTest.cs"), gasPrice, gasLimit);
                Script contractCreateScript = new Script(contractCarrier.Serialize());
                var txBuildContext = new TransactionBuildContext(new WalletAccountReference("mywallet", "account 0"),
                        new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList(), "123456")
                {
                    MinConfirmations = 101,
                    FeeType = FeeType.High,
                };

                var trx = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);
                // Equivalent to what happens in 'SendTransaction' on WalletController
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(trx);
                scSender.GenerateSmartContractStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                // sync both nodes
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));
                IContractStateRepository senderState = (IContractStateRepository) scSender.FullNode.Services.ServiceProvider.GetService(typeof(IContractStateRepository));
                IContractStateRepository receiverState = (IContractStateRepository) scReceiver.FullNode.Services.ServiceProvider.GetService(typeof(IContractStateRepository));
                uint160 newContractAddress = trx.GetNewContractAddress();
                Assert.NotNull(senderState.GetCode(newContractAddress));
                // Up to this point we're good! Just need to work out what happens in the consensus validator
                // Then seriously, create a new network
                // And adjust validation rules

                var test = receiverState.GetRoot();
                var test2 = senderState.GetRoot();
                receiverState.SyncToRoot(senderState.GetRoot());

                var receiverState2 = receiverState.GetSnapshotTo(senderState.GetRoot());

                Assert.NotNull(receiverState.GetCode(newContractAddress));
                Assert.NotNull(receiverState.GetCode(newContractAddress));
            }
        }
    }
}
