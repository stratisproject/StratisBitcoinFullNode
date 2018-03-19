using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Controllers;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
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

                // Create a token contract
                ulong gasPrice = 1;
                int vmVersion = 1;
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
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(trx);
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                scSender.GenerateSmartContractStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));
                ContractStateRepositoryRoot senderState = scSender.FullNode.NodeService<ContractStateRepositoryRoot>();
                ContractStateRepositoryRoot receiverState = scReceiver.FullNode.NodeService<ContractStateRepositoryRoot>();
                uint160 tokenContractAddress = trx.GetNewContractAddress();
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                contractCarrier = SmartContractCarrier.CreateContract(vmVersion, GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/TransferTest.cs"), gasPrice, gasLimit);
                contractCreateScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(new WalletAccountReference("mywallet", "account 0"),
                        new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList(), "123456")
                {
                    MinConfirmations = 101,
                    FeeType = FeeType.High,
                };
                trx = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(trx);
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                scSender.GenerateSmartContractStratisWithMiner(1);
                uint160 transferContractAddress = trx.GetNewContractAddress();
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                scSender.FullNode.MempoolManager().Clear();

                contractCarrier = SmartContractCarrier.CallContract(1, transferContractAddress, "Test", gasPrice, gasLimit);
                Script contractCallScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(new WalletAccountReference("mywallet", "account 0"),
                    new[] { new Recipient { Amount = 1000, ScriptPubKey = contractCallScript } }.ToList(), "123456")
                {
                    MinConfirmations = 101,
                    FeeType = FeeType.High,
                };
                trx = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(trx);
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                scSender.GenerateSmartContractStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));
                Assert.Equal((ulong) 900, senderState.GetCurrentBalance(transferContractAddress));
            }
        }

        [Fact]
        public void SendAndReceiveSmartContractTransactionsUsingController()
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
                var addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = scSender.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                scReceiver.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                scSender.GenerateSmartContractStratisWithMiner(maturity + 5);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 105 * 50, total);

                var smartContractsController = scSender.FullNode.NodeService<SmartContractsController>();
                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = "account 0",
                    GasLimit = "10000",
                    GasPrice = "1",
                    Amount = "0",
                    ContractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/Demo.cs").ToHexString(),
                    FeeAmount = "30000",
                    Password = "123456",
                    WalletName = "mywallet"
                };
                JsonResult result = (JsonResult)smartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                var walletController = scSender.FullNode.NodeService<WalletController>();

                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                walletController.SendTransaction(new SendTransactionRequest
                {
                    Hex = response.Hex
                });
                TestHelper.WaitLoop(() => scReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                scReceiver.GenerateSmartContractStratisWithMiner(2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                var callRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = "account 0",
                    GasLimit = "10000",
                    GasPrice = "1",
                    Amount = "0",
                    MethodName = "Increment",
                    ContractAddress = response.NewContractAddress,
                    FeeAmount = "30000",
                    Password = "123456",
                    WalletName = "mywallet"
                };
                result = (JsonResult)smartContractsController.BuildCallSmartContractTransaction(callRequest);
                var callResponse = (BuildCallContractTransactionResponse)result.Value;
                walletController.SendTransaction(new SendTransactionRequest
                {
                    Hex = callResponse.Hex
                });
                TestHelper.WaitLoop(() => scReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                scReceiver.GenerateSmartContractStratisWithMiner(2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));
            }
        }
    }
}
