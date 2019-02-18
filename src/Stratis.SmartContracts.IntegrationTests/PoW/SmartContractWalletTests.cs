using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.Networks;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoW
{
    public sealed class SmartContractWalletTests
    {
        private const string WalletName = "mywallet";
        private const string Password = "password";
        private const string AccountName = "account 0";
        private readonly IMethodParameterStringSerializer methodParameterStringSerializer;

        public SmartContractWalletTests()
        {
            this.methodParameterStringSerializer = new MethodParameterStringSerializer(new SmartContractsRegTest());
        }

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
                var maturity = (int)scSender.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity;
                TestHelper.MineBlocks(scSender.CoreNode, maturity + 5);
                chain.WaitForAllNodesToSync();
                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long)scSender.WalletSpendableBalance);

                // Send coins to receiver.
                HdAddress address = scReceiver.GetUnusedAddress();
                scSender.SendTransaction(address.ScriptPubKey, Money.COIN * 100);
                scReceiver.WaitMempoolCount(1);
                TestHelper.WaitLoop(() => (long) scReceiver.WalletSpendableBalance == Money.COIN * 100, waitTimeSeconds:10); // Give the wallet a bit of time to process receiving the transaction

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

            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode scSender = chain.Nodes[0];
                MockChainNode scReceiver = chain.Nodes[1];

                // Mining adds coins to wallet.
                var maturity = (int)scSender.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity;
                TestHelper.MineBlocks(scSender.CoreNode, maturity + 5);
                chain.WaitForAllNodesToSync();
                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long)scSender.WalletSpendableBalance);

                // Create a token contract.
                ulong gasPrice = SmartContractMempoolValidator.MinGasPrice;
                var gasLimit = (RuntimeObserver.Gas)(SmartContractFormatLogic.GasLimitMaximum / 2);

                // Create a transfer token contract.
                var compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTest.cs");
                Assert.True(compilationResult.Success);

                // Broadcast the token transaction to the network.
                var createTransactionResponse = scSender.SendCreateContractTransaction(compilationResult.Compilation, 0, feeAmount: 0.001M, gasPrice: gasPrice,
                    gasLimit: gasLimit);

                // Wait for the token transaction to be picked up by the mempool.
                scSender.WaitMempoolCount(1);

                // Mine the token transaction and wait for it to sync.
                scSender.MineBlocks(1);

                // Ensure that both nodes have the contract.
                var tokenContractAddress = createTransactionResponse.NewContractAddress;

                // Ensure that both nodes have the contract.
                Assert.NotNull(scSender.GetCode(tokenContractAddress));
                Assert.NotNull(scReceiver.GetCode(tokenContractAddress));

                // Create a call contract transaction which will transfer funds.
                scSender.SendCallContractTransaction("Test", createTransactionResponse.NewContractAddress, 0.00001M,
                    feeAmount: 0.001M, gasPrice: gasPrice, gasLimit: gasLimit);

                scSender.WaitMempoolCount(1);

                // Mine the transaction.
                scSender.MineBlocks(1);

                // The balance should now reflect the transfer.
                Assert.Equal((ulong)900, scSender.GetContractBalance(tokenContractAddress));
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

        [Retry]
        public void SendAndReceiveSmartContractTransactionsUsingController()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];

                // Mine some coins so we have balance
                int maturity = (int)sender.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity;
                sender.MineBlocks(maturity + 1);
                int spendable = GetSpendableBlocks(maturity + 1, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long)sender.WalletSpendableBalance);

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2;

                var response = sender.SendCreateContractTransaction(compilationResult.Compilation, 0, feeAmount: 0.001M,
                    gasPrice: SmartContractMempoolValidator.MinGasPrice, gasLimit: gasLimit);
                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);

                // Check receipt was stored and can be retrieved.
                var receiptResponse = sender.GetReceipt(response.TransactionId.ToString());
                Assert.True(receiptResponse.Success);
                Assert.Equal(response.NewContractAddress, receiptResponse.NewContractAddress);
                Assert.Null(receiptResponse.To);
                Assert.Equal(sender.MinerAddress.Address, receiptResponse.From);

                var storageRequestResult = sender.GetStorageValue(response.NewContractAddress, "TestSave");
                Assert.Equal("Hello, smart contract world!", Encoding.UTF8.GetString(storageRequestResult));

                var ownerRequestResult = sender.GetStorageValue(response.NewContractAddress, "Owner");
                Assert.NotEmpty(ownerRequestResult);

                var counterRequestResult = sender.GetStorageValue(response.NewContractAddress, "Counter");
                Assert.Equal(12345, BitConverter.ToInt32(counterRequestResult));

                var callResponse = sender.SendCallContractTransaction("Increment", response.NewContractAddress, 0,
                    feeAmount: 0.001M,
                    gasPrice: SmartContractMempoolValidator.MinGasPrice, gasLimit: gasLimit);

                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);

                counterRequestResult = sender.GetStorageValue(response.NewContractAddress, "Counter");
                Assert.Equal(12346, BitConverter.ToInt32(counterRequestResult));

                // Check receipt was stored and can be retrieved.
                receiptResponse = sender.GetReceipt(callResponse.TransactionId.ToString());
                Assert.True(receiptResponse.Success);
                Assert.Null(receiptResponse.NewContractAddress);
                Assert.Equal(response.NewContractAddress, receiptResponse.To);
                Assert.Equal(sender.MinerAddress.Address, receiptResponse.From);

                // Test serialization
                // TODO: When refactoring integration tests, move this to the one place and test all types, from method param to storage to serialization.

                sender.SendCallContractTransaction(
                    "TestSerializer",
                    response.NewContractAddress,
                    0,
                    feeAmount: 0.001M,
                    gasPrice: SmartContractMempoolValidator.MinGasPrice,
                    gasLimit: gasLimit);

                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);

                // Would have only saved if execution completed successfully
                counterRequestResult = sender.GetStorageValue(response.NewContractAddress, "Int32");
                Assert.Equal(12345, BitConverter.ToInt32(counterRequestResult));
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
                Assert.True(scBlockHeader.LogsBloom.Test(response.NewContractAddress.ToAddress(sender.CoreNode.FullNode.Network).ToBytes()));
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
        public void MockChain_Lottery()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];

                node1.MineBlocks(1);

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Lottery.cs");
                Assert.True(compilationResult.Success);

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
                node2.WaitMempoolCount(1);
                node2.MineBlocks(1);
                Assert.NotNull(node2.GetCode(response.NewContractAddress));
                Assert.NotNull(node1.GetCode(response.NewContractAddress));

                // Both users join
                BuildCallContractTransactionResponse callResponse = node1.SendCallContractTransaction("Join", response.NewContractAddress, 1);
                node2.WaitMempoolCount(1);
                node2.MineBlocks(1);
                Assert.Equal(node1.MinerAddress.Address.ToUint160(node1.CoreNode.FullNode.Network).ToBytes(), node1.GetStorageValue(response.NewContractAddress, "Player0"));

                callResponse = node2.SendCallContractTransaction("Join", response.NewContractAddress, 1);
                node1.WaitMempoolCount(1);
                node1.MineBlocks(1);
                Assert.Equal(node2.MinerAddress.Address.ToUint160(node2.CoreNode.FullNode.Network).ToBytes(), node2.GetStorageValue(response.NewContractAddress, "Player1"));

                // Select a winner
                callResponse = node1.SendCallContractTransaction("SelectWinner", response.NewContractAddress, 1);
                node2.WaitMempoolCount(1);
                node2.MineBlocks(1);
                uint winner = BitConverter.ToUInt32(node1.GetStorageValue(response.NewContractAddress, "WinningNumber"));

                MockChainNode winningNode = winner == 0 ? node1 : node2;
                MockChainNode losingNode = winner == 1 ? node1 : node2;

                // Ensure loser can't claim
                callResponse = losingNode.SendCallContractTransaction("Claim", response.NewContractAddress, 0);
                node2.WaitMempoolCount(1);
                node2.MineBlocks(1);
                ReceiptResponse receipt = node2.GetReceipt(callResponse.TransactionId.ToString());
                Assert.False(receipt.Success);

                // Ensure winner can claim
                callResponse = winningNode.SendCallContractTransaction("Claim", response.NewContractAddress, 0);
                node2.WaitMempoolCount(1);
                node2.MineBlocks(1);
                receipt = node2.GetReceipt(callResponse.TransactionId.ToString());
                Assert.True(receipt.Success);
                Assert.Equal(0uL, node2.GetContractBalance(response.NewContractAddress));
            }
        }

        [Fact]
        public void MockChain_NonFungibleToken()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];

                node1.MineBlocks(1);

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/NonFungibleToken.cs");
                Assert.True(compilationResult.Success);

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = node1.SendCreateContractTransaction(compilationResult.Compilation, 0);
                node2.WaitMempoolCount(1);
                node2.MineBlocks(1);
                Assert.NotNull(node2.GetCode(response.NewContractAddress));
                Assert.NotNull(node1.GetCode(response.NewContractAddress));

                string[] parameters = new string[]
                {
                    this.methodParameterStringSerializer.Serialize(1uL)
                };

                ILocalExecutionResult result = node1.CallContractMethodLocally("OwnerOf", response.NewContractAddress, 0, parameters);
                uint160 senderAddressUint160 = node1.MinerAddress.Address.ToUint160(node1.CoreNode.FullNode.Network);
                uint160 returnedAddressUint160 = ((Address)result.Return).ToUint160();
                Assert.Equal(senderAddressUint160, returnedAddressUint160);

                // Send tokenId 1 to a new owner
                parameters = new string[]
                {
                    this.methodParameterStringSerializer.Serialize(node1.MinerAddress.Address.ToAddress(node1.CoreNode.FullNode.Network)),
                    this.methodParameterStringSerializer.Serialize(node2.MinerAddress.Address.ToAddress(node1.CoreNode.FullNode.Network)),
                    this.methodParameterStringSerializer.Serialize(1uL)
                };
                BuildCallContractTransactionResponse callResponse = node1.SendCallContractTransaction("TransferFrom", response.NewContractAddress, 0, parameters);
                node2.WaitMempoolCount(1);
                node2.MineBlocks(1);

                parameters = new string[]
                {
                    this.methodParameterStringSerializer.Serialize(1uL)
                };
                result = node1.CallContractMethodLocally("OwnerOf", response.NewContractAddress, 0, parameters);
                uint160 receiverAddressUint160 = node2.MinerAddress.Address.ToUint160(node1.CoreNode.FullNode.Network);
                returnedAddressUint160 = ((Address)result.Return).ToUint160();
                Assert.Equal(receiverAddressUint160, returnedAddressUint160);

                IList<ReceiptResponse> receipts = node1.GetReceipts(response.NewContractAddress, "Transfer");
                Assert.Single(receipts);
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
                int maturity = (int)sender.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity;
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

        /// <summary>
        /// https://github.com/jbevain/cecil/issues/555
        /// </summary>
        [Fact]
        public void MockChain_AssemblyDoesntHang()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                // Mine some coins so we have balance
                int maturity = (int)sender.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity;
                sender.MineBlocks(maturity + 1);
                int spendable = GetSpendableBlocks(maturity + 1, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long)sender.WalletSpendableBalance);

                // Get hanging file
                byte[] bytes = File.ReadAllBytes("Modules/Hang");

                // Send create with value, and ensure balance is stored.
                BuildCreateContractTransactionResponse sendResponse = sender.SendCreateContractTransaction(bytes, 30);
                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);

                // Code didn't actually deploy.
                ReceiptResponse receipt = sender.GetReceipt(sendResponse.TransactionId.ToString());
                Assert.False(receipt.Success);

                // Can still progress - node didn't hang.
                sender.MineBlocks(1);
            }
        }

        [Fact]
        public void Many_LinkedTransactions_In_One_Block()
        {
            const int txsToLink = 10;

            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode node1 = chain.Nodes[0];

                // Mine only to maturity + 1 AKA only one transaction can be spent
                node1.MineBlocks((int)node1.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity + 1);

                // Send a bunch of transactions to be mined in the next block - wallet will arrange them so they each use the previous change output as their input
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                for (int i = 0; i < txsToLink; i++)
                {
                    BuildCreateContractTransactionResponse sendResponse = node1.SendCreateContractTransaction(compilationResult.Compilation, 1);
                    Assert.True(sendResponse.Success);
                }

                node1.WaitMempoolCount(txsToLink);
                node1.MineBlocks(1);

                NBitcoin.Block lastBlock = node1.GetLastBlock();
                Assert.Equal(txsToLink + 1, lastBlock.Transactions.Count);

                // Each transaction is indeed spending the output of the transaction before
                for (int i = 2; i < txsToLink; i++)
                {
                    Assert.Equal(lastBlock.Transactions[i - 1].GetHash(), lastBlock.Transactions[i].Inputs[0].PrevOut.Hash);
                }
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
                int maturity = (int)sender.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity;
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
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation, 0, new string[] { "7#20" }, gasLimit: 10_001);

                // Never reaches mempool.
                Thread.Sleep(3000);
                Assert.Empty(sender.CoreNode.CreateRPCClient().GetRawMempool());
            }
        }

        [Fact]
        public void SendAndReceiveLocalSmartContractTransactionsUsingController()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                int maturity = (int)sender.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity;
                sender.MineBlocks(maturity + 5);

                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long)sender.WalletSpendableBalance);

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2;

                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation, amount: 0, feeAmount: 0.001M, gasPrice: SmartContractMempoolValidator.MinGasPrice, gasLimit: gasLimit);
                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);

                var counterResult = sender.GetStorageValue(response.NewContractAddress, "Counter");

                Assert.Equal(12345, BitConverter.ToInt32(counterResult));

                var callResponse = sender.CallContractMethodLocally("Increment", response.NewContractAddress, 0,
                    gasPrice: SmartContractMempoolValidator.MinGasPrice, gasLimit: gasLimit);

                // Check that the locally executed transaction returns the correct results
                Assert.Equal(12346, callResponse.Return);
                Assert.False(callResponse.Revert);
                Assert.True(callResponse.GasConsumed > 0);
                Assert.Null(callResponse.ErrorMessage);
                Assert.NotNull(callResponse.InternalTransfers);

                receiver.MineBlocks(2);

                // Check that the on-chain storage has not changed after mining
                counterResult = sender.GetStorageValue(response.NewContractAddress, "Counter");

                Assert.Equal(12345, BitConverter.ToInt32(counterResult));
            }
        }

        [Fact]
        public void SendAndReceiveLocalSmartContractPropertyCallTransactionsUsingController()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                int maturity = (int)sender.CoreNode.FullNode.Network.Consensus.CoinbaseMaturity;
                sender.MineBlocks(maturity + 5);

                int spendable = GetSpendableBlocks(maturity + 5, maturity);
                Assert.Equal(Money.COIN * spendable * 50, (long)sender.WalletSpendableBalance);

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs");
                Assert.True(compilationResult.Success);

                ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2;

                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation, amount: 0, feeAmount: 0.001M, gasPrice: SmartContractMempoolValidator.MinGasPrice, gasLimit: gasLimit);
                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);

                var localCallResponse = sender.CallContractMethodLocally("Counter", response.NewContractAddress, 0,
                    gasPrice: SmartContractMempoolValidator.MinGasPrice, gasLimit: gasLimit);

                // Check that the locally executed transaction returns the correct results
                Assert.Equal(12345, localCallResponse.Return);
                Assert.False(localCallResponse.Revert);
                Assert.True(localCallResponse.GasConsumed > 0);
                Assert.Null(localCallResponse.ErrorMessage);
                Assert.NotNull(localCallResponse.InternalTransfers);

                receiver.MineBlocks(2);

                // Check that the on-chain storage has not changed after mining
                var counterResult = sender.GetStorageValue(response.NewContractAddress, "Counter");

                Assert.Equal(12345, BitConverter.ToInt32(counterResult));

                // Call increment and check return value on receipt
                BuildCallContractTransactionResponse callResponse = sender.SendCallContractTransaction("Increment", response.NewContractAddress, 0);
                sender.WaitMempoolCount(1);
                sender.MineBlocks(1);

                ReceiptResponse receipt = sender.GetReceipt(callResponse.TransactionId.ToString());
                Assert.Equal("12346", receipt.ReturnValue);
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