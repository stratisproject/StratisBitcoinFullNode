﻿using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class SmartContractWalletOnPosNetworkTests
    {
        private const string WalletName = "mywallet";
        private const string Password = "123456";
        private const string Passphrase = "test";
        private const string AccountName = "account 0";

        [Fact]
        public void SendAndReceiveSmartContractTransactionsOnPosNetwork()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode scSender = builder.CreateSmartContractPosNode();
                CoreNode scReceiver = builder.CreateSmartContractPosNode();

                builder.StartAll();

                scSender.NotInIBD();
                scReceiver.NotInIBD();

                scSender.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);
                scReceiver.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);

                var maturity = (int)scSender.FullNode.Network.Consensus.CoinbaseMaturity;
                HdAddress senderAddress = TestHelper.MineBlocks(scSender, WalletName, Password, AccountName, maturity + 5).AddressUsed;

                // Wait for block repo for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // The mining should add coins to the wallet.
                var total = scSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 6 * 50, total);

                // Create a token contract
                ulong gasPrice = 1;
                int vmVersion = 1;
                Gas gasLimit = (Gas)5000;
                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTestPos.cs");
                Assert.True(compilationResult.Success);

                var contractCarrier = ContractCarrier.CreateContract(vmVersion, compilationResult.Compilation, gasPrice, gasLimit);

                var contractCreateScript = new Script(contractCarrier.Serialize());
                var txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    ChangeAddress = senderAddress,
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    WalletPassword = Password,
                    Recipients = new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList()
                };

                // Build the transfer contract transaction
                var transferContractTransaction = BuildTransferContractTransaction(scSender, txBuildContext);

                // Add the smart contract transaction to the mempool to be mined.
                scSender.AddToStratisMempool(transferContractTransaction);

                // Ensure the smart contract transaction is in the mempool.
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the token transaction and wait for it sync
                scSender.GenerateStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // Sync to the receiver node 
                scSender.CreateRPCClient().AddNode(scReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // Ensure that boths nodes has the contract
                IStateRepositoryRoot senderState = scSender.FullNode.NodeService<IStateRepositoryRoot>();
                IStateRepositoryRoot receiverState = scReceiver.FullNode.NodeService<IStateRepositoryRoot>();
                IAddressGenerator addressGenerator = scSender.FullNode.NodeService<IAddressGenerator>();

                uint160 tokenContractAddress = addressGenerator.GenerateAddress(transferContractTransaction.GetHash(), 0);
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                // Create a transfer token contract
                compilationResult = ContractCompiler.CompileFile("SmartContracts/TransferTestPos.cs");
                Assert.True(compilationResult.Success);
                contractCarrier = ContractCarrier.CreateContract(vmVersion, compilationResult.Compilation, gasPrice, gasLimit);
                contractCreateScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    ChangeAddress = senderAddress,
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    WalletPassword = Password,
                    Recipients = new[] { new Recipient { Amount = 0, ScriptPubKey = contractCreateScript } }.ToList()
                };

                // Build the transfer contract transaction
                transferContractTransaction = BuildTransferContractTransaction(scSender, txBuildContext);

                // Add the smart contract transaction to the mempool to be mined.
                scSender.AddToStratisMempool(transferContractTransaction);

                // Wait for the token transaction to be picked up by the mempool
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                scSender.GenerateStratisWithMiner(1);

                // Ensure the node is synced
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));

                // Ensure both nodes are synced with each other
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                tokenContractAddress = addressGenerator.GenerateAddress(transferContractTransaction.GetHash(), 0); // nonce is 0 for user contract creation.
                Assert.NotNull(senderState.GetCode(tokenContractAddress));
                Assert.NotNull(receiverState.GetCode(tokenContractAddress));
                scSender.FullNode.MempoolManager().Clear();

                // Create a call contract transaction which will transfer funds
                contractCarrier = ContractCarrier.CallContract(1, tokenContractAddress, "Test", gasPrice, gasLimit);
                Script contractCallScript = new Script(contractCarrier.Serialize());
                txBuildContext = new TransactionBuildContext(scSender.FullNode.Network)
                {
                    AccountReference = new WalletAccountReference(WalletName, AccountName),
                    ChangeAddress = senderAddress,
                    MinConfirmations = maturity,
                    FeeType = FeeType.High,
                    WalletPassword = Password,
                    Recipients = new[] { new Recipient { Amount = 1000, ScriptPubKey = contractCallScript } }.ToList()
                };

                // Build the transfer contract transaction
                var callContractTransaction = BuildTransferContractTransaction(scSender, txBuildContext);

                // Add the smart contract transaction to the mempool to be mined.
                scSender.AddToStratisMempool(callContractTransaction);

                // Wait for the token transaction to be picked up by the mempool
                TestHelper.WaitLoop(() => scSender.CreateRPCClient().GetRawMempool().Length > 0);
                scSender.GenerateStratisWithMiner(1);

                // Ensure the nodes are synced
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(scSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(scReceiver, scSender));

                // The balance should now reflect the transfer
                Assert.Equal((ulong)900, senderState.GetCurrentBalance(tokenContractAddress));
            }
        }

        private Transaction BuildTransferContractTransaction(CoreNode scSender, TransactionBuildContext txBuildContext)
        {
            Transaction transferContractTransaction = scSender.FullNode.NodeService<IWalletTransactionHandler>().BuildTransaction(txBuildContext);

            var updatedTransaction = scSender.FullNode.Network.CreateTransaction();
            updatedTransaction.Time = (uint)scSender.FullNode.NodeService<IDateTimeProvider>().GetAdjustedTimeAsUnixTimestamp();

            foreach (var txIn in transferContractTransaction.Inputs)
            {
                updatedTransaction.AddInput(new TxIn(txIn.PrevOut, scSender.MinerSecret.ScriptPubKey));
            }

            foreach (var txOut in transferContractTransaction.Outputs)
            {
                updatedTransaction.AddOutput(new TxOut(txOut.Value, txOut.ScriptPubKey));
            }

            List<ICoin> coins = new List<ICoin>();
            foreach (var txin in updatedTransaction.Inputs)
            {
                coins.Add(new Coin(txin.PrevOut, new TxOut()
                {
                    ScriptPubKey = txin.ScriptSig
                }));
            }

            updatedTransaction.Sign(scSender.FullNode.Network, scSender.MinerSecret, coins[0]);

            return updatedTransaction;
        }
    }
}