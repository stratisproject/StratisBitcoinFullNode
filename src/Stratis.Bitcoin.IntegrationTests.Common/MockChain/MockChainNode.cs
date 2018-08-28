﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.IntegrationTests.Common.MockChain
{
    /// <summary>
    /// Facade for CoreNode.
    /// </summary>
    public class MockChainNode
    {
        private const string WalletName = "mywallet";
        private const string Password = "123456";
        private const string Passphrase = "passphrase";
        private const string AccountName = "account 0";

        /// <summary>
        /// Chain this node is part of.
        /// </summary>
        private readonly MockChain chain;

        // Services on the node. Used to retrieve information about the state of the network.
        private readonly SmartContractsController smartContractsController;
        private readonly SmartContractWalletController smartContractWalletController;
        private readonly ContractStateRepositoryRoot stateRoot;
        private readonly IBlockStoreCache blockStore;

        /// <summary>
        /// Reference to the complex underlying node object.
        /// </summary>
        internal CoreNode CoreNode { get; }

        /// <summary>
        /// The address that all new coins are mined to.
        /// </summary>
        public HdAddress MinerAddress { get; }

        public IEnumerable<UnspentOutputReference> SpendableTransactions
        {
            get
            {
                return this.CoreNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName);
            }
        }

        public Money WalletSpendableBalance
        {
            get
            {
                return this.SpendableTransactions.Sum(s => s.Transaction.Amount);
            }
        }

        /// <summary>
        /// Whether this node is fully synced.
        /// </summary>
        public bool IsSynced
        {
            get { return TestHelper.IsNodeSynced(this.CoreNode); }
        }

        public MockChainNode(CoreNode coreNode, MockChain chain)
        {
            this.CoreNode = coreNode;
            this.chain = chain;
            // Set up address and mining
            this.CoreNode.NotInIBD();
            this.CoreNode.FullNode.WalletManager().CreateWallet(Password, WalletName, Passphrase);
            this.MinerAddress = this.CoreNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
            Features.Wallet.Wallet wallet = this.CoreNode.FullNode.WalletManager().GetWalletByName(WalletName);
            Key key = wallet.GetExtendedPrivateKeyForAddress(Password, this.MinerAddress).PrivateKey;
            this.CoreNode.SetDummyMinerSecret(new BitcoinSecret(key, this.CoreNode.FullNode.Network));
            // Set up services for later
            this.smartContractWalletController = this.CoreNode.FullNode.NodeService<SmartContractWalletController>();
            this.smartContractsController = this.CoreNode.FullNode.NodeService<SmartContractsController>();
            this.stateRoot = this.CoreNode.FullNode.NodeService<ContractStateRepositoryRoot>();
            this.blockStore = this.CoreNode.FullNode.NodeService<IBlockStoreCache>();
        }

        /// <summary>
        /// Mine the given number of blocks. The block reward will go to this node's MinerAddress.
        /// </summary>
        /// <param name="num"></param>
        public void MineBlocks(int num)
        {
            this.CoreNode.GenerateStratisWithMiner(num);
            this.chain.WaitForAllNodesToSync();
        }

        public HdAddress GetUnusedAddress()
        {
            return this.CoreNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, AccountName));
        }

        public WalletSendTransactionModel SendTransaction(Script scriptPubKey, Money amount)
        {
            var txBuildContext = new TransactionBuildContext(this.chain.Network)
            {
                AccountReference = new WalletAccountReference(WalletName, AccountName),
                MinConfirmations = 1,
                FeeType = FeeType.Medium,
                WalletPassword = Password,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = scriptPubKey } }.ToList()
            };

            Transaction trx = (this.CoreNode.FullNode.NodeService<IWalletTransactionHandler>() as SmartContractWalletTransactionHandler).BuildTransaction(txBuildContext);

            // Broadcast to the other node.
            JsonResult response = (JsonResult) this.smartContractWalletController.SendTransaction(new SendTransactionRequest(trx.ToHex()));
            return (WalletSendTransactionModel) response.Value;
        }

        /// <summary>
        /// Sends a create contract transaction. Note that before this transaction can be mined it will need to reach the mempool.
        /// You will likely want to call 'WaitMempoolCount' after this.
        /// </summary>
        public BuildCreateContractTransactionResponse SendCreateContractTransaction(
            byte[] contractCode,
            double amount,
            string[] parameters = null,
            ulong gasLimit = 10000,
            ulong gasPrice = 1,
            double feeAmount = 0.01)
        {
            var request = new BuildCreateContractTransactionRequest
            {
                Amount = amount.ToString(),
                AccountName = AccountName,
                ContractCode = contractCode.ToHexString(),
                FeeAmount = feeAmount.ToString(),
                GasLimit = gasLimit.ToString(),
                GasPrice = gasPrice.ToString(),
                Parameters = parameters,
                Password = Password,
                Sender = this.MinerAddress.Address,
                WalletName = WalletName
            };
            JsonResult response = (JsonResult)this.smartContractsController.BuildAndSendCreateSmartContractTransaction(request);
            return (BuildCreateContractTransactionResponse)response.Value;
        }

        /// <summary>
        /// Retrieves receipts for all cases where a specific event was logged in a specific contract.
        /// </summary>
        public IList<ReceiptResponse> GetReceipts(string contractAddress, string eventName)
        {
            JsonResult response = (JsonResult)this.smartContractsController.ReceiptSearch(contractAddress, eventName).Result;
            return (IList<ReceiptResponse>) response.Value;
        }

        /// <summary>
        /// Sends a call contract transaction. Note that before this transaction can be mined it will need to reach the mempool.
        /// You will likely want to call 'WaitMempoolCount' after this.
        /// </summary>
        public BuildCallContractTransactionResponse SendCallContractTransaction(
            string methodName,
            string contractAddress,
            double amount,
            string[] parameters = null,
            ulong gasLimit = 10000,
            ulong gasPrice = 1,
            double feeAmount = 0.01)
        {
            var request = new BuildCallContractTransactionRequest
            {
                AccountName = AccountName,
                Amount = amount.ToString(),
                ContractAddress = contractAddress,
                FeeAmount = feeAmount.ToString(),
                GasLimit = gasLimit.ToString(),
                GasPrice = gasPrice.ToString(),
                MethodName = methodName,
                Parameters = parameters,
                Password = Password,
                Sender = this.MinerAddress.Address,
                WalletName = WalletName
            };
            JsonResult response = (JsonResult)this.smartContractsController.BuildAndSendCallSmartContractTransaction(request);
            return (BuildCallContractTransactionResponse)response.Value;
        }

        public ulong GetContractBalance(string contractAddress)
        {
            return this.stateRoot.GetCurrentBalance(new Address(contractAddress).ToUint160(this.CoreNode.FullNode.Network));
        }

        /// <summary>
        /// Get the bytecode stored at a particular contract address.
        /// </summary>
        public byte[] GetCode(string contractAddress)
        {
            return this.stateRoot.GetCode(new Address(contractAddress).ToUint160(this.CoreNode.FullNode.Network));
        }

        /// <summary>
        /// Get the bytes stored at a particular key in a particular address.
        /// </summary>
        public byte[] GetStorageValue(string contractAddress, string key)
        {
            return this.stateRoot.GetStorageValue(
                new Address(contractAddress).ToUint160(this.CoreNode.FullNode.Network), Encoding.UTF8.GetBytes(key));
        }

        /// <summary>
        /// Get the last block mined. AKA the current tip.
        /// </summary
        public Block GetLastBlock()
        {
            return this.blockStore.GetBlockAsync(this.CoreNode.FullNode.Chain.Tip.HashBlock).Result;
        }

        /// <summary>
        /// Wait until the amount of transactions in the mempool reaches the given number.
        /// </summary>
        public void WaitMempoolCount(int num)
        {
            TestHelper.WaitLoop(() => this.CoreNode.CreateRPCClient().GetRawMempool().Length >= num);
        }
    }
}
