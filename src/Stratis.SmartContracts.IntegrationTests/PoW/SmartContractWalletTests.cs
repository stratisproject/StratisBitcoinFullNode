using System;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.Networks;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Stratis.SmartContracts.Executor.Reflection.Local;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Stratis.SmartContracts.IntegrationTests.PoW.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoW
{
    public sealed class SmartContractWalletTests
    {
        private const string WalletName = "mywallet";
        private const string Password = "password";
        //private const string Passphrase = "passphrase";
        private const string AccountName = "account 0";

        /// <summary>
        /// These are the same tests as in WalletTests.cs, just using the smart contract classes instead.
        /// </summary>
        [Fact]
        public void SendAndReceiveCorrectly()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode scSender = chain.Nodes[0];
                MockChainNode scReceiver = chain.Nodes[1];

                // Mining adds coins to wallet.
                var maturity = (int)chain.Network.Consensus.CoinbaseMaturity;
                TestHelper.MineBlocks(scSender.CoreNode, maturity + 5);
                chain.WaitForAllNodesToSync();
                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long)scSender.WalletSpendableBalance);

                // Send coins to receiver.
                HdAddress address = scReceiver.GetUnusedAddress();
                scSender.SendTransaction(address.ScriptPubKey, Money.COIN * 100);
                scReceiver.WaitMempoolCount(1);
                Assert.Equal(Money.COIN * 100, (long)scReceiver.WalletSpendableBalance); // Balance is added (unconfirmed)

                // Transaction is in chain in last block.
                scReceiver.MineBlocks(1);
                var lastBlock = scReceiver.GetLastBlock();
                Assert.Equal(scReceiver.SpendableTransactions.First().Transaction.BlockHash, lastBlock.GetHash());
            }
        }

        [Fact]
        public void SendAndReceiveSmartContractTransactions()
        {
            NetworkRegistration.Register(new SmartContractsRegTest());

            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractPowNode().WithWallet().Start();
                CoreNode scReceiver = builder.CreateSmartContractPowNode().WithWallet().Start();

                var callDataSerializer = new CallDataSerializer(new ContractPrimitiveSerializer(scSender.FullNode.Network));

                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                HdAddress senderAddress = TestHelper.MineBlocks(scSender, maturity + 5).AddressUsed;

                // The mining should add coins to the wallet.
                int spendableBlocks = GetSpendableBlocks(maturity + 5, maturity);
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * spendableBlocks * 50, total);

                // Create a token contract.
                ulong gasPrice = SmartContractMempoolValidator.MinGasPrice;
                int vmVersion = 1;
                Gas gasLimit = (Gas)(SmartContractFormatRule.GasLimitMaximum / 2);
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
                Assert.True(compilationResult.Success);

                var contractTxData = new ContractTxData(vmVersion, gasPrice, gasLimit, compilationResult.Compilation);

                var contractCreateScript = new Script(callDataSerializer.Serialize(contractTxData));
                var txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    MinConfirmations = maturity,
                    TransactionFee = new Money(1, MoneyUnit.BTC),
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
                TestHelper.MineBlocks(scSender, 1);

                // Sync to the receiver node.
                TestHelper.ConnectAndSync(scSender, scReceiver);

                // Ensure that both nodes have the contract.
                IStateRepositoryRoot senderState = scSender.FullNode.NodeService<IStateRepositoryRoot>();
                IStateRepositoryRoot receiverState = scReceiver.FullNode.NodeService<IStateRepositoryRoot>();
                IAddressGenerator addressGenerator = scSender.FullNode.NodeService<IAddressGenerator>();

                uint160 tokenContractAddress = addressGenerator.GenerateAddress(transferContractTransaction.GetHash(), 0);
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                // Create a transfer token contract.
                compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
                Assert.True(compilationResult.Success);
                contractTxData = new ContractTxData(vmVersion, gasPrice, gasLimit, compilationResult.Compilation);
                contractCreateScript = new Script(callDataSerializer.Serialize(contractTxData));
                txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    MinConfirmations = maturity,
                    TransactionFee = new Money(1, MoneyUnit.BTC),
                    FeeType = FeeType.High,
                    WalletPassword = Password,
                    Recipients = new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList()
                };

                // Broadcast the token transaction to the network.
                transferContractTransaction = (scSender.FullNode.NodeService<IWalletTransactionHandler>() as SmartContractWalletTransactionHandler).BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);

                // Wait for the token transaction to be picked up by the mempool.
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.MineBlocks(scSender, 1);

                // Ensure both nodes are synced with each other.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Ensure that both nodes have the contract.
                senderState = scSender.FullNode.NodeService<IStateRepositoryRoot>();
                receiverState = scReceiver.FullNode.NodeService<IStateRepositoryRoot>();
                tokenContractAddress = addressGenerator.GenerateAddress(transferContractTransaction.GetHash(), 0);
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                // Create a call contract transaction which will transfer funds.
                contractTxData = new ContractTxData(1, gasPrice, gasLimit, tokenContractAddress, "Test");
                Script contractCallScript = new Script(callDataSerializer.Serialize(contractTxData));
                txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    MinConfirmations = maturity,
                    TransactionFee = new Money(1, MoneyUnit.BTC),
                    FeeType = FeeType.High,
                    WalletPassword = Password,
                    Recipients = new[] { new Recipient { Amount = 1000, ScriptPubKey = contractCallScript } }.ToList()
                };

                // Broadcast the token transaction to the network.
                transferContractTransaction = (scSender.FullNode.NodeService<IWalletTransactionHandler>() as SmartContractWalletTransactionHandler).BuildTransaction(txBuildContext);
                scSender.FullNode.NodeService<IBroadcasterManager>().BroadcastTransactionAsync(transferContractTransaction);
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the transaction.
                TestHelper.MineBlocks(scSender, 1);

                // Ensure the nodes are synced
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
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractPowNode().WithWallet().Start();

                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;

                HdAddress addr = TestHelper.MineBlocks(scSender, maturity + 5).AddressUsed;

                int spendableBlocks = GetSpendableBlocks(maturity + 5, maturity);
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * spendableBlocks * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();

                SmartContractWalletController senderWalletController = scSender.FullNode.NodeService<SmartContractWalletController>();
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = 10_000,
                    GasPrice = 1,
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
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractPowNode().WithWallet().Start();
                CoreNode scReceiver = builder.CreateSmartContractPowNode().WithWallet().Start();

                int maturity = (int)scReceiver.FullNode.Network.Consensus.CoinbaseMaturity;

                HdAddress addr = TestHelper.MineBlocks(scSender, maturity + 5).AddressUsed;

                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * spendable * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();
                SmartContractWalletController senderWalletController = scSender.FullNode.NodeService<SmartContractWalletController>();
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                Gas gasLimit = (Gas)(SmartContractFormatRule.GasLimitMaximum / 2);

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = gasLimit,
                    GasPrice = SmartContractMempoolValidator.MinGasPrice,
                    ContractCode = compilationResult.Compilation.ToHexString(),
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                JsonResult result = (JsonResult)senderSmartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                TestHelper.Connect(scSender, scReceiver);

                SmartContractSharedSteps.SendTransaction(scSender, scReceiver, senderWalletController, response.Hex);
                TestHelper.MineBlocks(scReceiver, 2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Check receipt was stored and can be retrieved.
                var receiptResponse = (ReceiptResponse)((JsonResult)senderSmartContractsController.GetReceipt(response.TransactionId.ToString())).Value;
                Assert.True(receiptResponse.Success);
                Assert.Equal(response.NewContractAddress, receiptResponse.NewContractAddress);
                Assert.Null(receiptResponse.To);
                Assert.Equal(addr.Address, receiptResponse.From);

                string storageRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "TestSave",
                    DataType = MethodParameterDataType.String
                })).Value;
                Assert.Equal("Hello, smart contract world!", storageRequestResult);

                string ownerRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Owner",
                    DataType = MethodParameterDataType.Address
                })).Value;
                Assert.NotEmpty(ownerRequestResult);

                string counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Counter",
                    DataType = MethodParameterDataType.Int
                })).Value;
                Assert.Equal("12345", counterRequestResult);

                var callRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = gasLimit,
                    GasPrice = SmartContractMempoolValidator.MinGasPrice,
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

                SmartContractSharedSteps.SendTransaction(scSender, scReceiver, senderWalletController, callResponse.Hex);
                TestHelper.MineBlocks(scReceiver, 2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Counter",
                    DataType = MethodParameterDataType.Int
                })).Value;
                Assert.Equal("12346", counterRequestResult);

                // Check receipt was stored and can be retrieved.
                receiptResponse = (ReceiptResponse)((JsonResult)senderSmartContractsController.GetReceipt(callResponse.TransactionId.ToString())).Value;
                Assert.True(receiptResponse.Success);
                Assert.Null(receiptResponse.NewContractAddress);
                Assert.Equal(response.NewContractAddress, receiptResponse.To);
                Assert.Equal(addr.Address, receiptResponse.From);

                // Test serialization
                // TODO: When refactoring integration tests, move this to the one place and test all types, from method param to storage to serialization.

                var serializationRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = gasLimit,
                    GasPrice = SmartContractMempoolValidator.MinGasPrice,
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
                SmartContractSharedSteps.SendTransaction(scSender, scReceiver, senderWalletController, serializationResponse.Hex);
                TestHelper.MineBlocks(scReceiver, 2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Would have only saved if execution completed successfully
                counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress.ToString(),
                    StorageKey = "Int32",
                    DataType = MethodParameterDataType.Int
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
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                sender.MineBlocks(1);

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Auction.cs");
                Assert.True(compilationResult.Success);

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation, 0, new string[] { "7#20" });
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(1);
                Assert.NotNull(receiver.GetCode(response.NewContractAddress));
                Assert.NotNull(sender.GetCode(response.NewContractAddress));

                // Test that the contract address, event name, and logging values are available in the bloom.
                var scBlockHeader = receiver.GetLastBlock().Header as SmartContractBlockHeader;
                Assert.True(scBlockHeader.LogsBloom.Test(response.NewContractAddress.ToAddress(chain.Network).ToBytes()));
                Assert.True(scBlockHeader.LogsBloom.Test(Encoding.UTF8.GetBytes("Created")));
                Assert.True(scBlockHeader.LogsBloom.Test(BitConverter.GetBytes((ulong)20)));
                // And sanity test that a non-indexed field and random value is not available in bloom.
                Assert.False(scBlockHeader.LogsBloom.Test(Encoding.UTF8.GetBytes(sender.MinerAddress.Address)));
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
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                // Mine some coins so we have balance
                int maturity = (int)chain.Network.Consensus.CoinbaseMaturity;
                sender.MineBlocks(maturity + 1);
                int spendable = GetSpendableBlocks(maturity + 1, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long)sender.WalletSpendableBalance);

                // Compile file
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                // Send create with value, and ensure balance is stored.
                BuildCreateContractTransactionResponse sendResponse = sender.SendCreateContractTransaction(compilationResult.Compilation, 30);
                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);

                Assert.Equal((ulong)30 * 100_000_000, sender.GetContractBalance(sendResponse.NewContractAddress));
            }
        }

        [Fact]
        public void Cant_Send_Create_With_OnlyBaseFee()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                // Mine some coins so we can send 100 coins
                int maturity = (int)chain.Network.Consensus.CoinbaseMaturity;
                sender.MineBlocks(maturity + 3);
                int spendable = GetSpendableBlocks(maturity + 1, maturity);
                Assert.Equal(Money.COIN * spendable * 150, (long)sender.WalletSpendableBalance);

                // Give the receiver 100 coins
                Money receiverBalance = new Money(100, MoneyUnit.BTC);
                sender.SendTransaction(receiver.MinerAddress.ScriptPubKey, receiverBalance);
                sender.MineBlocks(1);
                Assert.Equal(receiver.WalletSpendableBalance, receiverBalance);

                uint256 currentHash = sender.GetLastBlock().GetHash();

                // Attempt to create contract with too little gas
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Auction.cs");
                Assert.True(compilationResult.Success);
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation, 0, new string[] { "7#20" }, gasLimit:10_001);

                // Never reaches mempool.
                Thread.Sleep(3000);
                Assert.Empty(sender.CoreNode.CreateRPCClient().GetRawMempool());
            }
        }

        [Fact]
        public void SendAndReceiveLocalSmartContractTransactionsUsingController()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractPowNode().WithWallet().Start();
                CoreNode scReceiver = builder.CreateSmartContractPowNode().WithWallet().Start();

                int maturity = (int)scReceiver.FullNode.Network.Consensus.CoinbaseMaturity;

                HdAddress addr = TestHelper.MineBlocks(scSender, maturity + 5).AddressUsed;

                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * spendable * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();
                SmartContractWalletController senderWalletController = scSender.FullNode.NodeService<SmartContractWalletController>();
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                Gas gasLimit = (Gas)(SmartContractFormatRule.GasLimitMaximum / 2);

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = gasLimit,
                    GasPrice = SmartContractMempoolValidator.MinGasPrice,
                    ContractCode = compilationResult.Compilation.ToHexString(),
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                JsonResult result = (JsonResult)senderSmartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                TestHelper.Connect(scSender, scReceiver);

                SmartContractSharedSteps.SendTransaction(scSender, scReceiver, senderWalletController, response.Hex);
                TestHelper.MineBlocks(scReceiver, 2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                string counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress,
                    StorageKey = "Counter",
                    DataType = MethodParameterDataType.Int
                })).Value;

                Assert.Equal("12345", counterRequestResult);

                var callRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = gasLimit,
                    GasPrice = SmartContractMempoolValidator.MinGasPrice,
                    Amount = "0",
                    MethodName = "Increment",
                    ContractAddress = response.NewContractAddress,
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                result = (JsonResult)senderSmartContractsController.LocalCallSmartContractTransaction(callRequest);
                var callResponse = (ILocalExecutionResult)result.Value;
                
                // Check that the locally executed transaction returns the correct results
                Assert.Equal(12346, callResponse.Return);
                Assert.False(callResponse.Revert);
                Assert.True(callResponse.GasConsumed > 0);
                Assert.Null(callResponse.ErrorMessage);
                Assert.NotNull(callResponse.InternalTransfers);

                TestHelper.MineBlocks(scReceiver, 2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Check that the on-chain storage has not changed after mining
                counterRequestResult = (string)((JsonResult)senderSmartContractsController.GetStorage(new GetStorageRequest
                {
                    ContractAddress = response.NewContractAddress,
                    StorageKey = "Counter",
                    DataType = MethodParameterDataType.Int
                })).Value;

                Assert.Equal("12345", counterRequestResult);
            }
        }

        [Fact]
        public void SendAndReceiveLocalSmartContractPropertyCallTransactionsUsingController()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractPowNode().WithWallet().Start();
                CoreNode scReceiver = builder.CreateSmartContractPowNode().WithWallet().Start();

                int maturity = (int)scReceiver.FullNode.Network.Consensus.CoinbaseMaturity;

                HdAddress addr = TestHelper.MineBlocks(scSender, maturity + 5).AddressUsed;

                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * spendable * 50, total);

                SmartContractsController senderSmartContractsController = scSender.FullNode.NodeService<SmartContractsController>();
                SmartContractWalletController senderWalletController = scSender.FullNode.NodeService<SmartContractWalletController>();
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                Gas gasLimit = (Gas)(SmartContractFormatRule.GasLimitMaximum / 2);

                var buildRequest = new BuildCreateContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = gasLimit,
                    GasPrice = SmartContractMempoolValidator.MinGasPrice,
                    ContractCode = compilationResult.Compilation.ToHexString(),
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                JsonResult result = (JsonResult)senderSmartContractsController.BuildCreateSmartContractTransaction(buildRequest);
                var response = (BuildCreateContractTransactionResponse)result.Value;
                TestHelper.Connect(scSender, scReceiver);

                SmartContractSharedSteps.SendTransaction(scSender, scReceiver, senderWalletController, response.Hex);
                TestHelper.MineBlocks(scReceiver, 2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Make a call request where the MethodName is the name of a property
                var callRequest = new BuildCallContractTransactionRequest
                {
                    AccountName = AccountName,
                    GasLimit = gasLimit,
                    GasPrice = SmartContractMempoolValidator.MinGasPrice,
                    Amount = "0",
                    MethodName = "Counter",
                    ContractAddress = response.NewContractAddress,
                    FeeAmount = "0.001",
                    Password = Password,
                    WalletName = WalletName,
                    Sender = addr.Address
                };

                result = (JsonResult)senderSmartContractsController.LocalCallSmartContractTransaction(callRequest);
                var callResponse = (ILocalExecutionResult)result.Value;

                // Check that the locally executed transaction returns the correct results
                Assert.Equal(12345, callResponse.Return);
                Assert.False(callResponse.Revert);
                Assert.True(callResponse.GasConsumed > 0);
                Assert.Null(callResponse.ErrorMessage);
                Assert.NotNull(callResponse.InternalTransfers);

                TestHelper.MineBlocks(scReceiver, 2);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));
            }
        }

        /// <summary>
        /// Given an amount of blocks and a maturity, how many blocks have spendable coinbase / coinstakes.
        /// </summary>
        private static int GetSpendableBlocks(int mined, int maturity)
        {
            return mined - maturity;
        }
    }
}