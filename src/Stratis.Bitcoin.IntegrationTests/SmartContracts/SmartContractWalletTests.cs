using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    public sealed class SmartContractWalletTests
    {
        private const string WalletName = "mywallet";
        private const string Password = "123456";
        private const string AccountName = "account 0";

        /// <summary>
        /// This is the same test in WalletTests.cs, just using all of the smart contract classes
        /// </summary>
        [Fact]
        public void SendAndReceiveCorrectly()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractNode();
                CoreNode scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                Mnemonic mnemonic1 = scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                Mnemonic mnemonic2 = scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                scSender.GenerateStratisWithMiner(maturity + 5);
                // wait for block repo for block sync to work

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // the mining should add coins to the wallet
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * (maturity + 5) * 50, total);

                // sync both nodes
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // send coins to the receiver
                HdAddress sendto = scReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                var txBuildContext = new TransactionBuildContext(new WalletAccountReference(WalletName, AccountName), new[] { new Recipient { Amount = Money.COIN * 100, ScriptPubKey = sendto.ScriptPubKey } }.ToList(), Password)
                {
                    MinConfirmations = maturity,
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
                scSender.GenerateBlocks(1, new List<Transaction>(new[] { scSender.FullNode.Network.CreateTransaction(trx.ToBytes()) }));
                scSender.GenerateStratisWithMiner(1);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                TestHelper.WaitLoop(() => maturity + 6 == scReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);
            }
        }

        [Fact]
        public void SendAndReceiveSmartContractTransactions()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractNode();
                CoreNode scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                scSender.GenerateStratisWithMiner(maturity + 5);

                // Wait for block repo for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // The mining should add coins to the wallet.
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * (maturity + 5) * 50, total);

                // Create a token contract
                ulong gasPrice = 1;
                int vmVersion = 1;
                Gas gasLimit = (Gas)2000;
                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
                Assert.True(compilationResult.Success);

                var contractCarrier = SmartContractCarrier.CreateContract(vmVersion, compilationResult.Compilation, gasPrice, gasLimit);

                var contractCreateScript = new Script(contractCarrier.Serialize());
                var txBuildContext = new TransactionBuildContext(new WalletAccountReference(WalletName, AccountName), new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList(), Password)
                {
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    DustPrevention = false
                };

                Transaction transferContractTransaction = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);

                // Broadcast the token transaction to the network
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);

                // Wait for the token transaction to be picked up by the mempool
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the token transaction and wait for it sync
                scSender.GenerateStratisWithMiner(1);
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
                compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
                Assert.True(compilationResult.Success);
                contractCarrier = SmartContractCarrier.CreateContract(vmVersion, compilationResult.Compilation, gasPrice, gasLimit);
                contractCreateScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(new WalletAccountReference(WalletName, AccountName), new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList(), Password)
                {
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    DustPrevention = false
                };

                // Broadcast the token transaction to the network
                transferContractTransaction = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);

                // Wait for the token transaction to be picked up by the mempool
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                scSender.GenerateStratisWithMiner(1);

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
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    DustPrevention = false
                };

                // Broadcast the token transaction to the network
                transferContractTransaction = scSender.FullNode.WalletTransactionHandler().BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the transaction
                scSender.GenerateStratisWithMiner(1);

                // Ensure the nodes are synced
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // The balance should now reflect the transfer
                Assert.Equal((ulong)900, senderState.GetCurrentBalance(tokenContractAddress));
            }
        }

        /*
         * We need to be careful that the transactions built by the TransactionBuilder don't try to include all UTXOs as inputs,
         * as this leads to issues with coinbase-immaturity.
         *
         * Until we update the SmartContractsController to retrieve only mature transactions, we need this test.
         */

        [Fact]
        public void SmartContractsController_Builds_Transaction_With_Minimum_Inputs()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractNode();
                builder.StartAll();

                scSender.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;
                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                scSender.GenerateStratisWithMiner(maturity + 5);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * (maturity + 5) * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();

                WalletController senderWalletController = scSender.FullNode.NodeService<WalletController>();
                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = "10000",
                    GasPrice = "1",
                    ContractCode = compilationResult.Compilation.ToHexString(),
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                JsonResult result = (JsonResult)senderSmartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                var transaction = scSender.FullNode.Network.CreateTransaction(response.Hex);
                Assert.Single(transaction.Inputs);
            }
        }

        [Fact]
        public void SendAndReceiveSmartContractTransactionsUsingController()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractNode();
                CoreNode scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                scReceiver.SetDummyMinerSecret(new BitcoinSecret(key, scReceiver.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                scSender.GenerateStratisWithMiner(maturity + 5);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * (maturity + 5) * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();

                WalletController senderWalletController = scSender.FullNode.NodeService<WalletController>();
                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = "10000",
                    GasPrice = "1",
                    ContractCode = compilationResult.Compilation.ToHexString(),
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                JsonResult result = (JsonResult)senderSmartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);

                SmartContractSharedSteps.SendTransactionAndMine(scSender, scReceiver, senderWalletController, response.Hex);

                var receiptStorage = scReceiver.FullNode.NodeService<ISmartContractReceiptStorage>();
                Assert.NotNull(receiptStorage.GetReceipt(response.TransactionId));

                // Check wallet history is updating correctly
                result = (JsonResult)senderWalletController.GetHistory(new WalletHistoryRequest
                {
                    AccountName = AccountName,
                    WalletName = WalletName
                });
                var walletHistoryModel = (WalletHistoryModel)result.Value;
                Assert.Single(walletHistoryModel.AccountsHistoryModel.First().TransactionsHistory.Where(x => x.Type == TransactionItemType.Send));

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
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };
                result = (JsonResult)senderSmartContractsController.BuildCallSmartContractTransaction(callRequest);
                var callResponse = (BuildCallContractTransactionResponse)result.Value;

                SmartContractSharedSteps.SendTransactionAndMine(scSender, scReceiver, senderWalletController, callResponse.Hex);

                counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Counter",
                    DataType = SmartContractDataType.Int
                })).Value;
                Assert.Equal("12346", counterRequestResult);

                // Check wallet history again
                result = (JsonResult)senderWalletController.GetHistory(new WalletHistoryRequest
                {
                    AccountName = AccountName,
                    WalletName = WalletName
                });
                walletHistoryModel = (WalletHistoryModel)result.Value;
                Assert.Equal(2, walletHistoryModel.AccountsHistoryModel.First().TransactionsHistory.Where(x => x.Type == TransactionItemType.Send).Count());

                // Check receipts
                var receiptResponse = (JsonResult)senderSmartContractsController.GetReceipt(callResponse.TransactionId.ToString());
                var receiptModel = (ReceiptModel)receiptResponse.Value;
                Assert.True(receiptModel.Successful);
            }
        }

        /*
        * Tests the most basic end-to-end functionality of the Auction contract. 
        * 
        * NOTE: This tests the situation where a contract leaves itself with a 0 balance, and
        * hence hits 'ClearUnspent' in TransactionCondenser.cs. If about to remove this test,
        * ensure that we have this case covered in SmartContractMinerTests.cs.
        */

        [Fact]
        public void AuctionTest()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractNode();
                CoreNode scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                scReceiver.SetDummyMinerSecret(new BitcoinSecret(key, scReceiver.FullNode.Network));

                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                scSender.GenerateStratisWithMiner(maturity + 5);

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * (maturity + 5) * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();
                WalletController senderWalletController = scSender.FullNode.NodeService<WalletController>();
                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/Auction.cs");
                Assert.True(compilationResult.Success);

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = "10000",
                    GasPrice = "1",
                    ContractCode = compilationResult.Compilation.ToHexString(),
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address,
                    Parameters = new string[] { "10#20" }
                };

                JsonResult result = (JsonResult)senderSmartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);

                SmartContractSharedSteps.SendTransactionAndMine(scSender, scReceiver, senderWalletController, response.Hex);

                // Contract deployed.
                ContractStateRepositoryRoot senderState = scSender.FullNode.NodeService<ContractStateRepositoryRoot>();
                string contractAddress = response.NewContractAddress.ToString();
                uint160 contractAddressUint160 = new Address(contractAddress).ToUint160(scSender.FullNode.Network);
                Assert.NotNull(senderState.GetCode(contractAddressUint160));

                var callRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = "10000",
                    GasPrice = "1",
                    Amount = "2",
                    MethodName = "Bid",
                    ContractAddress = response.NewContractAddress,
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };
                result = (JsonResult)senderSmartContractsController.BuildCallSmartContractTransaction(callRequest);
                var callResponse = (BuildCallContractTransactionResponse)result.Value;

                SmartContractSharedSteps.SendTransactionAndMine(scSender, scReceiver, senderWalletController, callResponse.Hex);

                // Here, test that Sender and HighestBidder are the same.
                var ownerBytes = senderState.GetStorageValue(contractAddressUint160, Encoding.UTF8.GetBytes("Owner"));
                var highestBidderBytes = senderState.GetStorageValue(contractAddressUint160, Encoding.UTF8.GetBytes("HighestBidder"));
                Assert.Equal(new uint160(ownerBytes), new uint160(highestBidderBytes));
                var hasEndedBytes = senderState.GetStorageValue(contractAddressUint160, Encoding.UTF8.GetBytes("HasEnded"));
                var endBlockBytes = senderState.GetStorageValue(contractAddressUint160, Encoding.UTF8.GetBytes("EndBlock"));
                bool hasEnded = BitConverter.ToBoolean(hasEndedBytes, 0);
                ulong endBlock = BitConverter.ToUInt64(endBlockBytes, 0);

                // Wait 20 blocks to end auction
                scReceiver.GenerateStratisWithMiner(20);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                var endAuctionRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = "10000",
                    GasPrice = "1",
                    Amount = "0",
                    MethodName = "AuctionEnd",
                    ContractAddress = response.NewContractAddress,
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                var endAuctionResult = (JsonResult)senderSmartContractsController.BuildCallSmartContractTransaction(endAuctionRequest);
                var endAuctionResponse = (BuildCallContractTransactionResponse)endAuctionResult.Value;

                SmartContractSharedSteps.SendTransactionAndMine(scSender, scReceiver, senderWalletController, endAuctionResponse.Hex);

                // Check that transaction did in fact go through with condensing tx as well
                var blockStoreCache = scSender.FullNode.NodeService<IBlockStoreCache>();
                var secondLastBlock = blockStoreCache.GetBlockAsync(scSender.FullNode.Chain.Tip.Previous.HashBlock).Result;
                Assert.Equal(3, secondLastBlock.Transactions.Count);
            }
        }

        [Fact]
        public void Create_WithFunds_Via_Controller()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractNode();
                CoreNode scReceiver = builder.CreateSmartContractNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                scReceiver.SetDummyMinerSecret(new BitcoinSecret(key, scReceiver.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                scSender.GenerateStratisWithMiner(maturity + 5);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * (maturity + 5) * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();
                WalletController senderWalletController = scSender.FullNode.NodeService<WalletController>();
                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    Amount = "30",
                    GasLimit = "10000",
                    GasPrice = "1",
                    ContractCode = compilationResult.Compilation.ToHexString(),
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                JsonResult result = (JsonResult)senderSmartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);

                SmartContractSharedSteps.SendTransactionAndMine(scSender, scReceiver, senderWalletController, response.Hex);

                ContractStateRepositoryRoot senderState = scSender.FullNode.NodeService<ContractStateRepositoryRoot>();
                Assert.Equal((ulong)30 * 100_000_000, senderState.GetCurrentBalance(new Address(response.NewContractAddress).ToUint160(new SmartContractsRegTest())));
            }
        }
    }
}