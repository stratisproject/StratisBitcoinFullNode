using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Tests.Common.MockChain
{
    /// <summary>
    /// Facade for CoreNode.
    /// </summary>
    public class MockChainNode
    {
        public readonly string WalletName = "mywallet";
        public readonly string Password = "password";
        public readonly string Passphrase = "passphrase";
        public readonly string AccountName = "account 0";

        // Services on the node. Used to retrieve information about the state of the network.
        private readonly SmartContractsController smartContractsController;
        private readonly SmartContractWalletController smartContractWalletController;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly IBlockStore blockStore;

        /// <summary>
        /// The chain / network this node is part of.
        /// </summary>
        private readonly IMockChain chain;

        /// <summary>
        /// Reference to the complex underlying node object.
        /// </summary>
        public CoreNode CoreNode { get; }

        /// <summary>
        /// The address that all new coins are mined to.
        /// </summary>
        public HdAddress MinerAddress { get; }

        /// <summary>
        /// The transactions available to be spent from this node's wallet.
        /// </summary>
        public IEnumerable<UnspentOutputReference> SpendableTransactions
        {
            get
            {
                return this.CoreNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(this.WalletName);
            }
        }

        /// <summary>
        /// The balance currently available to be spent by this node's wallet.
        /// </summary>
        public Money WalletSpendableBalance
        {
            get
            {
                return this.SpendableTransactions.Sum(s => s.Transaction.Amount);
            }
        }

        public MockChainNode(CoreNode coreNode, IMockChain chain, Mnemonic mnemonic = null)
        {
            this.CoreNode = coreNode;
            this.chain = chain;

            // Set up address and mining
            this.CoreNode.FullNode.WalletManager().CreateWallet(this.Password, this.WalletName, this.Passphrase, mnemonic);
            this.MinerAddress = this.CoreNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(this.WalletName, this.AccountName));
            Wallet wallet = this.CoreNode.FullNode.WalletManager().GetWalletByName(this.WalletName);
            Key key = wallet.GetExtendedPrivateKeyForAddress(this.Password, this.MinerAddress).PrivateKey;
            this.CoreNode.SetMinerSecret(new BitcoinSecret(key, this.CoreNode.FullNode.Network));

            // Set up services for later
            this.smartContractWalletController = this.CoreNode.FullNode.NodeService<SmartContractWalletController>();
            this.smartContractsController = this.CoreNode.FullNode.NodeService<SmartContractsController>();
            this.stateRoot = this.CoreNode.FullNode.NodeService<IStateRepositoryRoot>();
            this.blockStore = this.CoreNode.FullNode.NodeService<IBlockStore>();
        }

        /// <summary>
        /// Mine the given number of blocks. The block reward will go to this node's MinerAddress.
        /// </summary>
        public void MineBlocks(int amountOfBlocks)
        {
            TestHelper.MineBlocks(this.CoreNode, amountOfBlocks);
            this.chain.WaitForAllNodesToSync();
        }

        public ulong GetWalletAddressBalance(string walletAddress)
        {
            var jsonResult = (JsonResult) this.smartContractWalletController.GetAddressBalance(walletAddress);
            return (ulong) (decimal) jsonResult.Value;
        }

        /// <summary>
        /// Get an unused address that can be used to send funds to this node.
        /// </summary>
        public HdAddress GetUnusedAddress()
        {
            return this.CoreNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(this.WalletName, this.AccountName));
        }

        /// <summary>
        /// Send a normal transaction.
        /// </summary>
        public Result<WalletSendTransactionModel> SendTransaction(Script scriptPubKey, Money amount)
        {
            var txBuildContext = new TransactionBuildContext(this.CoreNode.FullNode.Network)
            {
                AccountReference = new WalletAccountReference(this.WalletName, this.AccountName),
                MinConfirmations = 1,
                FeeType = FeeType.Medium,
                WalletPassword = this.Password,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = scriptPubKey } }.ToList(),
                SelectedInputs = this.CoreNode.FullNode.WalletManager().GetSpendableInputsForAddress(this.WalletName, this.MinerAddress.Address), // Always send from the MinerAddress. Simplifies things.
                ChangeAddress = this.MinerAddress // yes this is unconventional, but helps us to keep the balance on the same addresses
            };

            Transaction trx = (this.CoreNode.FullNode.NodeService<IWalletTransactionHandler>() as SmartContractWalletTransactionHandler).BuildTransaction(txBuildContext);

            // Broadcast to the other node.

            IActionResult result = this.smartContractWalletController.SendTransaction(new SendTransactionRequest(trx.ToHex()));
            if (result is ErrorResult errorResult)
            {
                var errorResponse = (ErrorResponse)errorResult.Value;
                return Result.Fail<WalletSendTransactionModel>(errorResponse.Errors[0].Message);
            }

            JsonResult response = (JsonResult)result;
            return Result.Ok((WalletSendTransactionModel)response.Value);
        }

        /// <summary>
        /// Sends a create contract transaction. Note that before this transaction can be mined it will need to reach the mempool.
        /// You will likely want to call 'WaitMempoolCount' after this.
        /// </summary>
        public BuildCreateContractTransactionResponse SendCreateContractTransaction(
            byte[] contractCode,
            decimal amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            decimal feeAmount = 0.01M,
            string sender = null)
        {
            var request = new BuildCreateContractTransactionRequest
            {
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                AccountName = this.AccountName,
                ContractCode = contractCode.ToHexString(),
                FeeAmount = feeAmount.ToString(CultureInfo.InvariantCulture),
                GasLimit = gasLimit,
                GasPrice = gasPrice,
                Parameters = parameters,
                Password = this.Password,
                Sender = sender ?? this.MinerAddress.Address,
                WalletName = this.WalletName
            };
            JsonResult response = (JsonResult)this.smartContractsController.BuildAndSendCreateSmartContractTransaction(request);
            return (BuildCreateContractTransactionResponse)response.Value;
        }

        /// <summary>
        /// Sends a create contract transaction. Note that before this transaction can be mined it will need to reach the mempool.
        /// You will likely want to call 'WaitMempoolCount' after this.
        /// </summary>
        public BuildCreateContractTransactionResponse BuildCreateContractTransaction(
            byte[] contractCode,
            double amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            double feeAmount = 0.01,
            string sender = null)
        {
            var request = new BuildCreateContractTransactionRequest
            {
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                AccountName = this.AccountName,
                ContractCode = contractCode.ToHexString(),
                FeeAmount = feeAmount.ToString(CultureInfo.InvariantCulture),
                GasLimit = gasLimit,
                GasPrice = gasPrice,
                Parameters = parameters,
                Password = this.Password,
                Sender = sender ?? this.MinerAddress.Address,
                WalletName = this.WalletName
            };
            JsonResult response = (JsonResult)this.smartContractsController.BuildCreateSmartContractTransaction(request);
            return (BuildCreateContractTransactionResponse)response.Value;
        }

        /// <summary>
        /// Retrieves receipts for all cases where a specific event was logged in a specific contract.
        /// </summary>
        public IList<ReceiptResponse> GetReceipts(string contractAddress, string eventName)
        {
            JsonResult response = (JsonResult)this.smartContractsController.ReceiptSearch(contractAddress, eventName).Result;
            return (IList<ReceiptResponse>)response.Value;
        }

        public ReceiptResponse GetReceipt(string txHash)
        {
            JsonResult response = (JsonResult)this.smartContractsController.GetReceipt(txHash);
            return (ReceiptResponse)response.Value;
        }

        /// <summary>
        /// Sends a call contract transaction. Note that before this transaction can be mined it will need to reach the mempool.
        /// You will likely want to call 'WaitMempoolCount' after this.
        /// </summary>
        public BuildCallContractTransactionResponse SendCallContractTransaction(
            string methodName,
            string contractAddress,
            decimal amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            decimal feeAmount = 0.01M, 
            string sender = null)
        {
            var request = new BuildCallContractTransactionRequest
            {
                AccountName = this.AccountName,
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                ContractAddress = contractAddress,
                FeeAmount = feeAmount.ToString(CultureInfo.InvariantCulture),
                GasLimit = gasLimit,
                GasPrice = gasPrice,
                MethodName = methodName,
                Parameters = parameters,
                Password = this.Password,
                Sender = sender ?? this.MinerAddress.Address,
                WalletName = this.WalletName
            };

            JsonResult response = (JsonResult)this.smartContractsController.BuildAndSendCallSmartContractTransaction(request);

            return (BuildCallContractTransactionResponse)response.Value;
        }

        public ILocalExecutionResult CallContractMethodLocally(
            string methodName,
            string contractAddress,
            decimal amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            string sender = null)
        {
            var request = new LocalCallContractRequest
            {
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                ContractAddress = contractAddress,
                GasLimit = gasLimit,
                GasPrice = gasPrice,
                MethodName = methodName,
                Parameters = parameters,
                Sender = sender ?? this.MinerAddress.Address
            };
            JsonResult response = (JsonResult)this.smartContractsController.LocalCallSmartContractTransaction(request);
            return (ILocalExecutionResult) response.Value;
        }

        /// <summary>
        /// Get the balance of a particular contract address.
        /// </summary>
        public ulong GetContractBalance(string contractAddress)
        {
            return this.stateRoot.GetCurrentBalance(contractAddress.ToUint160(this.CoreNode.FullNode.Network));
        }

        /// <summary>
        /// Get the bytecode stored at a particular contract address.
        /// </summary>
        public byte[] GetCode(string contractAddress)
        {
            return this.stateRoot.GetCode(contractAddress.ToUint160(this.CoreNode.FullNode.Network));
        }

        /// <summary>
        /// Get the bytes stored at a particular key in a particular address.
        /// </summary>
        public byte[] GetStorageValue(string contractAddress, string key)
        {
            return this.stateRoot.GetStorageValue(contractAddress.ToUint160(this.CoreNode.FullNode.Network), Encoding.UTF8.GetBytes(key));
        }

        /// <summary>
        /// Get the last block mined. AKA the current tip.
        /// </summary
        public NBitcoin.Block GetLastBlock()
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
