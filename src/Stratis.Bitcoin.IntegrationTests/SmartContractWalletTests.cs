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

namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    public sealed class SmartContractWalletTests : IDisposable
    {
        private const string WalletName = "mywallet";
        private const string Password = "123456";
        private const string AccountName = "account 0";

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
                CoreNode scSender = builder.CreateSmartContractNode();
                CoreNode scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                Mnemonic mnemonic1 = scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                Mnemonic mnemonic2 = scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                scSender.GenerateSmartContractStratis(maturity + 5);
                // wait for block repo for block sync to work

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // the mining should add coins to the wallet
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 105 * 50, total);

                // sync both nodes
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // send coins to the receiver
                HdAddress sendto = scReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                var txBuildContext = new TransactionBuildContext(new WalletAccountReference(WalletName, AccountName), new[] { new Recipient { Amount = Money.COIN * 100, ScriptPubKey = sendto.ScriptPubKey } }.ToList(), Password)
                {
                    MinConfirmations = 101,
                    FeeType = FeeType.Medium
                };

                Transaction trx = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);

                // broadcast to the other node
                scSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => scReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.WaitLoop(() => scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

                var receivetotal = scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // generate two new blocks do the trx is confirmed
                scSender.GenerateSmartContractStratis(1, new List<Transaction>(new[] { trx.Clone() }));
                scSender.GenerateSmartContractStratis(1);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                TestHelper.WaitLoop(() => maturity + 6 == scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);
            }
        }

        [Fact]
        public void SendAndReceiveSmartContractTransactions()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode scSender = builder.CreateSmartContractNode();
                CoreNode scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                scSender.GenerateSmartContractStratisWithMiner(maturity + 5);

                // Wait for block repo for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // The mining should add coins to the wallet.
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 105 * 50, total);

                // Create a token contract
                ulong gasPrice = 1;
                int vmVersion = 1;
                Gas gasLimit = (Gas)2000;
                var contractCarrier = SmartContractCarrier.CreateContract(vmVersion, GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/TransferTest.cs"), gasPrice, gasLimit);
                var contractCreateScript = new Script(contractCarrier.Serialize());
                var txBuildContext = new TransactionBuildContext(new WalletAccountReference(WalletName, AccountName), new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList(), Password)
                {
                    MinConfirmations = 101,
                    FeeType = FeeType.High,
                };

                Transaction transferContractTransaction = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);

                // Broadcast the token transaction to the network
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);

                // Wait for the token transaction to be picked up by the mempool
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the token transaction and wait for it sync
                scSender.GenerateSmartContractStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // Sync to the receiver node 
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Ensure that boths nodes has the contract
                ContractStateRepositoryRoot senderState = scSender.FullNode.NodeService<ContractStateRepositoryRoot>();
                ContractStateRepositoryRoot receiverState = scReceiver.FullNode.NodeService<ContractStateRepositoryRoot>();
                uint160 tokenContractAddress = transferContractTransaction.GetNewContractAddress();
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                // Create a transfer token contract
                contractCarrier = SmartContractCarrier.CreateContract(vmVersion, GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/TransferTest.cs"), gasPrice, gasLimit);
                contractCreateScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(new WalletAccountReference(WalletName, AccountName), new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList(), Password)
                {
                    MinConfirmations = 101,
                    FeeType = FeeType.High,
                };

                // Broadcast the token transaction to the network
                transferContractTransaction = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);

                // Wait for the token transaction to be picked up by the mempool
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                scSender.GenerateSmartContractStratisWithMiner(1);

                // Ensure the node is synced
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // Ensure both nodes are synced with each other
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Ensure that boths nodes has the contract
                senderState = scSender.FullNode.NodeService<ContractStateRepositoryRoot>();
                receiverState = scReceiver.FullNode.NodeService<ContractStateRepositoryRoot>();
                tokenContractAddress = transferContractTransaction.GetNewContractAddress();
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                // Create a call contract transaction which will transfer funds
                contractCarrier = SmartContractCarrier.CallContract(1, tokenContractAddress, "Test", gasPrice, gasLimit);
                Script contractCallScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(new WalletAccountReference(WalletName, AccountName), new[] { new Recipient { Amount = 1000, ScriptPubKey = contractCallScript } }.ToList(), Password)
                {
                    MinConfirmations = 101,
                    FeeType = FeeType.High,
                };

                // Broadcast the token transaction to the network
                transferContractTransaction = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the transaction
                scSender.GenerateSmartContractStratisWithMiner(1);

                // Ensure the nodes are synced
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // The balance should now reflect the transfer
                Assert.Equal((ulong)900, senderState.GetCurrentBalance(tokenContractAddress));
            }
        }

        [Fact]
        public void SendAndReceiveSmartContractTransactionsUsingController()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode scSender = builder.CreateSmartContractNode();
                CoreNode scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                scReceiver.SetDummyMinerSecret(new BitcoinSecret(key, scReceiver.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                scSender.GenerateSmartContractStratisWithMiner(maturity + 5);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 105 * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();
                WalletController senderWalletController = scSender.FullNode.NodeService<WalletController>();

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = "10000",
                    GasPrice = "1",
                    Amount = "0",
                    ContractCode = GetFileDllHelper.GetAssemblyBytesFromFile("SmartContracts/StorageDemo.cs").ToHexString(),
                    FeeAmount = "30000",
                    Password = Password,
                    WalletName = WalletName
                };
                JsonResult result = (JsonResult)senderSmartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                senderWalletController.SendTransaction(new SendTransactionRequest
                {
                    Hex = response.Hex
                });
                TestHelper.WaitLoop(() => scReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                scReceiver.GenerateSmartContractStratisWithMiner(2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                string storageRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "TestSave",
                    DataType = SmartContractDataType.String
                })).Value;
                Assert.Equal("Hello, smart contract world!", storageRequestResult);

                string ownerRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Owner",
                    DataType = SmartContractDataType.Address
                })).Value;
                Assert.NotEmpty(ownerRequestResult);

                string counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Counter",
                    DataType = SmartContractDataType.Int
                })).Value;
                Assert.Equal("12345", counterRequestResult);

                var callRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = "10000",
                    GasPrice = "1",
                    Amount = "0",
                    MethodName = "Increment",
                    ContractAddress = response.NewContractAddress,
                    FeeAmount = "30000",
                    Password = Password,
                    WalletName = WalletName
                };
                result = (JsonResult)senderSmartContractsController.BuildCallSmartContractTransaction(callRequest);
                var callResponse = (BuildCallContractTransactionResponse)result.Value;
                senderWalletController.SendTransaction(new SendTransactionRequest
                {
                    Hex = callResponse.Hex
                });
                TestHelper.WaitLoop(() => scReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                scReceiver.GenerateSmartContractStratisWithMiner(2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Counter",
                    DataType = SmartContractDataType.Int
                })).Value;
                Assert.Equal("12346", counterRequestResult);
            }
        }
    }
}