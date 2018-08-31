using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.MockChain;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
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
        private const string Passphrase = "passphrase";
        private const string AccountName = "account 0";

        /// <summary>
        /// These are the same tests as in WalletTests.cs, just using the smart contract classes instead.
        /// </summary>
        [Fact]
        public void SendAndReceiveCorrectly()
        {
            using (MockChain chain = new MockChain(2))
            {
                MockChainNode scSender = chain.Nodes[0];
                MockChainNode scReceiver = chain.Nodes[1];

                // Mining adds coins to wallet.
                var maturity = (int) chain.Network.Consensus.CoinbaseMaturity;
                scSender.MineBlocks(maturity + 5);
                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long) scSender.WalletSpendableBalance);

                // Send coins to receiver.
                HdAddress address = scReceiver.GetUnusedAddress();
                scSender.SendTransaction(address.ScriptPubKey, Money.COIN * 100);
                scReceiver.WaitMempoolCount(1);
                Assert.Equal(Money.COIN * 100, (long) scReceiver.WalletSpendableBalance); // Balance is added (unconfirmed)

                // Transaction is in chain in last block.
                scReceiver.MineBlocks(1);
                var lastBlock = scReceiver.GetLastBlock();
                Assert.Equal(scReceiver.SpendableTransactions.First().Transaction.BlockHash, lastBlock.GetHash());
            }
        }

        [Fact]
        public void SendAndReceiveSmartContractTransactions()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractPowNode();
                CoreNode scReceiver = builder.CreateSmartContractPowNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                scSender.GenerateStratisWithMiner(maturity + 5);

                // Wait for block repo for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // The mining should add coins to the wallet.
                int spendableBlocks = GetSpendableBlocks(maturity + 5, maturity);
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * spendableBlocks * 50, total);

                // Create a token contract.
                ulong gasPrice = 1;
                int vmVersion = 1;
                Gas gasLimit = (Gas)2000;
                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
                Assert.True(compilationResult.Success);

                var contractCarrier = SmartContractCarrier.CreateContract(vmVersion, compilationResult.Compilation, gasPrice, gasLimit);

                var contractCreateScript = new Script(contractCarrier.Serialize());
                var txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    WalletPassword = Password,
                    Recipients = new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList()
                };

                Transaction transferContractTransaction = (scSender.FullNode.NodeService<IWalletTransactionHandler>() as SmartContractWalletTransactionHandler).BuildTransaction(txBuildContext);

                // Broadcast the token transaction to the network.
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);

                // Wait for the token transaction to be picked up by the mempool.
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the token transaction and wait for it to sync.
                scSender.GenerateStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // Sync to the receiver node.
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Ensure that both nodes have the contract.
                IContractStateRoot senderState = scSender.FullNode.NodeService<IContractStateRoot>();
                IContractStateRoot receiverState = scReceiver.FullNode.NodeService<IContractStateRoot>();
                IAddressGenerator addressGenerator = scSender.FullNode.NodeService<IAddressGenerator>();

                uint160 tokenContractAddress = addressGenerator.GenerateAddress(transferContractTransaction.GetHash(), 0);
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                // Create a transfer token contract.
                compilationResult = SmartContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
                Assert.True(compilationResult.Success);
                contractCarrier = SmartContractCarrier.CreateContract(vmVersion, compilationResult.Compilation, gasPrice, gasLimit);
                contractCreateScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    WalletPassword = Password,
                    Recipients = new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList()
                };

                // Broadcast the token transaction to the network.
                transferContractTransaction = (scSender.FullNode.NodeService<IWalletTransactionHandler>() as SmartContractWalletTransactionHandler).BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);

                // Wait for the token transaction to be picked up by the mempool.
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                scSender.GenerateStratisWithMiner(1);

                // Ensure the node is synced.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // Ensure both nodes are synced with each other.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Ensure that both nodes have the contract.
                senderState = scSender.FullNode.NodeService<IContractStateRoot>();
                receiverState = scReceiver.FullNode.NodeService<IContractStateRoot>();
                tokenContractAddress = addressGenerator.GenerateAddress(transferContractTransaction.GetHash(), 0);
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                // Create a call contract transaction which will transfer funds.
                contractCarrier = SmartContractCarrier.CallContract(1, tokenContractAddress, "Test", gasPrice, gasLimit);
                Script contractCallScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    WalletPassword = Password,
                    Recipients = new[] { new Recipient { Amount = 1000, ScriptPubKey = contractCallScript } }.ToList()
                };

                // Broadcast the token transaction to the network.
                transferContractTransaction = (scSender.FullNode.NodeService<IWalletTransactionHandler>() as SmartContractWalletTransactionHandler).BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the transaction.
                scSender.GenerateStratisWithMiner(1);

                // Ensure the nodes are synced
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // The balance should now reflect the transfer.
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
                CoreNode scSender = builder.CreateSmartContractPowNode();
                builder.StartAll();

                scSender.NotInIBD();

                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;
                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                scSender.GenerateStratisWithMiner(maturity + 5);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                int spendableBlocks = GetSpendableBlocks(maturity + 5, maturity);
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * spendableBlocks * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();

                SmartContractWalletController senderWalletController = scSender.FullNode.NodeService<SmartContractWalletController>();
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
                CoreNode scSender = builder.CreateSmartContractPowNode();
                CoreNode scReceiver = builder.CreateSmartContractPowNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                int maturity = (int)scReceiver.FullNode.Network.Consensus.CoinbaseMaturity;

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);
                HdAddress addr = scSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
                Features.Wallet.Wallet wallet = scSender.FullNode.WalletManager().GetWalletByName(WalletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(Password, addr).PrivateKey;

                scSender.SetDummyMinerSecret(new BitcoinSecret(key, scSender.FullNode.Network));
                scReceiver.SetDummyMinerSecret(new BitcoinSecret(key, scReceiver.FullNode.Network));

                scSender.GenerateStratisWithMiner(maturity + 5);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * spendable * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();
                SmartContractWalletController senderWalletController = scSender.FullNode.NodeService<SmartContractWalletController>();
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

                // Check wallet history is updating correctly.

                result = (JsonResult)senderWalletController.GetHistory(new WalletHistoryRequest
                {
                    AccountName = AccountName,
                    WalletName = WalletName
                });
                var walletHistoryModel = (WalletHistoryModel)result.Value;
                Assert.Single(walletHistoryModel.AccountsHistoryModel.First().TransactionsHistory.Where(x => x.Type == TransactionItemType.Send));

                // Check receipt was stored and can be retrieved.
                var receiptResponse = (ReceiptResponse) ((JsonResult)senderSmartContractsController.GetReceipt(response.TransactionId.ToString())).Value;
                Assert.True(receiptResponse.Success);
                Assert.Equal(response.NewContractAddress, receiptResponse.NewContractAddress);
                Assert.Null(receiptResponse.To);
                Assert.Equal(addr.Address, receiptResponse.From);

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

                // Check receipt was stored and can be retrieved.
                receiptResponse = (ReceiptResponse)((JsonResult)senderSmartContractsController.GetReceipt(callResponse.TransactionId.ToString())).Value;
                Assert.True(receiptResponse.Success);
                Assert.Null(receiptResponse.NewContractAddress);
                Assert.Equal(response.NewContractAddress, receiptResponse.To);
                Assert.Equal(addr.Address, receiptResponse.From);

                // Check wallet history again
                result = (JsonResult)senderWalletController.GetHistory(new WalletHistoryRequest
                {
                    AccountName = AccountName,
                    WalletName = WalletName
                });
                walletHistoryModel = (WalletHistoryModel)result.Value;
                Assert.Equal(2, walletHistoryModel.AccountsHistoryModel.First().TransactionsHistory.Where(x => x.Type == TransactionItemType.Send).Count());

                // Test serialization
                // TODO: When refactoring integration tests, move this to the one place and test all types, from method param to storage to serialization.

                var serializationRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = "10000",
                    GasPrice = "1",
                    Amount = "0",
                    MethodName = "TestSerializer",
                    ContractAddress = response.NewContractAddress,
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };
                result = (JsonResult)senderSmartContractsController.BuildCallSmartContractTransaction(serializationRequest);
                var serializationResponse = (BuildCallContractTransactionResponse)result.Value;
                SmartContractSharedSteps.SendTransactionAndMine(scSender, scReceiver, senderWalletController, serializationResponse.Hex);

                // Would have only saved if execution completed successfully
                counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Int32",
                    DataType = SmartContractDataType.Int
                })).Value;
                Assert.Equal("12345", counterRequestResult);
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
        public void MockChain_AuctionTest()
        {
            var network = new SmartContractsRegTest(); // ew hack. TODO: Expose from MockChain or MockChainNode.

            using (MockChain chain = new MockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                sender.MineBlocks(1);

                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/Auction.cs");
                Assert.True(compilationResult.Success);

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation, 0, new string[] { "10#20" });
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(1);
                Assert.NotNull(receiver.GetCode(response.NewContractAddress));
                Assert.NotNull(sender.GetCode(response.NewContractAddress));

                // Test that the contract address, event name, and logging values are available in the bloom.
                var scBlockHeader = receiver.GetLastBlock().Header as SmartContractBlockHeader;
                Assert.True(scBlockHeader.LogsBloom.Test(new Address(response.NewContractAddress).ToUint160(network).ToBytes()));
                Assert.True(scBlockHeader.LogsBloom.Test(Encoding.UTF8.GetBytes("Created")));
                Assert.True(scBlockHeader.LogsBloom.Test(BitConverter.GetBytes((ulong) 20)));
                // And sanity test that a random value is not available in bloom.
                Assert.False(scBlockHeader.LogsBloom.Test(Encoding.UTF8.GetBytes("RandomValue")));

                // Test that the event can be searched for...
                var receiptsFromSearch = sender.GetReceipts(response.NewContractAddress, "Created");
                Assert.Single(receiptsFromSearch);

                // Call contract and ensure owner is now highest bidder
                BuildCallContractTransactionResponse callResponse = sender.SendCallContractTransaction("Bid", response.NewContractAddress, 2);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(1);
                Assert.Equal(sender.GetStorageValue(response.NewContractAddress, "Owner"), sender.GetStorageValue(response.NewContractAddress, "HighestBidder"));

                // Wait 20 blocks and end auction and check for transaction to victor
                sender.MineBlocks(20);
                sender.SendCallContractTransaction("AuctionEnd", response.NewContractAddress, 0);
                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);
                NBitcoin.Block block = sender.GetLastBlock();
                Assert.Equal(3, block.Transactions.Count);
            }
        }

        [Fact]
        public void Create_WithFunds()
        {
            using (MockChain chain = new MockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                // Mine some coins so we have balance
                int maturity = (int) chain.Network.Consensus.CoinbaseMaturity;
                sender.MineBlocks(maturity + 1);
                int spendable = GetSpendableBlocks(maturity + 1, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long) sender.WalletSpendableBalance);

                // Compile file
                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                // Send create with value, and ensure balance is stored.
                BuildCreateContractTransactionResponse sendResponse = sender.SendCreateContractTransaction(compilationResult.Compilation, 30);
                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);
                Assert.Equal((ulong)30 * 100_000_000, sender.GetContractBalance(sendResponse.NewContractAddress));
            }
        }

        /// <summary>
        /// Given an amount of blocks and a maturity, how many blocks have spendable coinbase / coinstakes.
        /// </summary>
        private static int GetSpendableBlocks(int mined, int maturity)
        {
            return mined - (maturity - 1);
        }
    }
}