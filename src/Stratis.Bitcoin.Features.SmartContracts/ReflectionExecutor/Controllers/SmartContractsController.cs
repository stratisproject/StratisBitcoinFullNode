using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Decompilation;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using State = Stratis.SmartContracts.CLR.State;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    [Route("api/[controller]")]
    public class SmartContractsController : Controller
    {
        /// <summary>
        /// For consistency in retrieval of balances, and to ensure that smart contract transaction
        /// creation always works, as the retrieved transactions have always already been included in a block.
        /// </summary>
        private const int MinConfirmationsAllChecks = 1;

        private readonly IBroadcasterManager broadcasterManager;
        private readonly IBlockStore blockStore;
        private readonly ChainIndexer chainIndexer;
        private readonly CSharpContractDecompiler contractDecompiler;
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly IWalletManager walletManager;
        private readonly ISerializer serializer;
        private readonly IReceiptRepository receiptRepository;
        private readonly ILocalExecutor localExecutor;
        private readonly ISmartContractTransactionService smartContractTransactionService;
        private readonly IConnectionManager connectionManager;

        public SmartContractsController(IBroadcasterManager broadcasterManager,
            IBlockStore blockStore,
            ChainIndexer chainIndexer,
            CSharpContractDecompiler contractDecompiler,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            IStateRepositoryRoot stateRoot,
            IWalletManager walletManager,
            ISerializer serializer,
            IReceiptRepository receiptRepository,
            ILocalExecutor localExecutor,
            ISmartContractTransactionService smartContractTransactionService,
            IConnectionManager connectionManager)
        {
            this.stateRoot = stateRoot;
            this.contractDecompiler = contractDecompiler;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
            this.serializer = serializer;
            this.receiptRepository = receiptRepository;
            this.localExecutor = localExecutor;
            this.smartContractTransactionService = smartContractTransactionService;
            this.connectionManager = connectionManager;
        }

        /// <summary>
        /// Gets the bytecode for a smart contract as a hexadecimal string. The bytecode is decompiled to
        /// C# source, which is returned as well. Be aware, it is the bytecode which is being executed,
        /// so this is the "source of truth".
        /// </summary>
        ///
        /// <param name="address">The address of the smart contract to retrieve as bytecode and C# source.</param>
        ///
        /// <returns>A response object containing the bytecode and the decompiled C# code.</returns>
        [Route("code")]
        [HttpGet]
        public IActionResult GetCode([FromQuery]string address)
        {
            uint160 addressNumeric = address.ToUint160(this.network);
            byte[] contractCode = this.stateRoot.GetCode(addressNumeric);

            if (contractCode == null || !contractCode.Any())
            {
                return this.Json(new GetCodeResponse
                {
                    Message = string.Format("No contract execution code exists at {0}", address)
                });
            }

            string typeName = this.stateRoot.GetContractType(addressNumeric);

            Result<string> sourceResult = this.contractDecompiler.GetSource(contractCode);

            return this.Json(new GetCodeResponse
            {
                Message = string.Format("Contract execution code retrieved at {0}", address),
                Bytecode = contractCode.ToHexString(),
                Type = typeName,
                CSharp = sourceResult.IsSuccess ? sourceResult.Value : sourceResult.Error // Show the source, or the reason why the source couldn't be retrieved.
            });
        }

        /// <summary>
        /// Gets the balance of a smart contract in STRAT (or the sidechain coin). This method only works for smart contract addresses. 
        /// </summary>
        /// 
        /// <param name="address">The address of the smart contract to retrieve the balance for.</param>
        /// 
        /// <returns>The balance of a smart contract in STRAT (or the sidechain coin).</returns>
        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance([FromQuery]string address)
        {
            uint160 addressNumeric = address.ToUint160(this.network);
            ulong balance = this.stateRoot.GetCurrentBalance(addressNumeric);
            Money moneyBalance = Money.Satoshis(balance);
            return this.Json(moneyBalance.ToString(false));
        }

        /// <summary>
        /// Gets a single piece of smart contract data, which was stored as a key–value pair using the
        /// SmartContract.PersistentState property. 
        /// The method performs a lookup in the smart contract
        /// state database for the supplied smart contract address and key.
        /// The value associated with the given key, deserialized for the specified data type, is returned.
        /// If the key does not exist or deserialization fails, the method returns the default value for
        /// the specified type.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to perform a retrieve stored data request.</param>
        ///
        /// <returns>A single piece of stored smart contract data.</returns>
        [Route("storage")]
        [HttpGet]
        public IActionResult GetStorage([FromQuery] GetStorageRequest request)
        {
            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODELSTATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            uint160 addressNumeric = request.ContractAddress.ToUint160(this.network);
            byte[] storageValue = this.stateRoot.GetStorageValue(addressNumeric, Encoding.UTF8.GetBytes(request.StorageKey));

            if (storageValue == null)
            {
                return this.Json(new
                {
                    Message = string.Format("No data at storage with key {0}", request.StorageKey)
                });
            }

            // Interpret the storage bytes as an object of the given type
            object interpretedStorageValue = this.InterpretStorageValue(request.DataType, storageValue);

            // Use MethodParamStringSerializer to serialize the interpreted object to a string
            string serialized = MethodParameterStringSerializer.Serialize(interpretedStorageValue, this.network);
            return this.Json(serialized);
        }

        /// <summary>
        /// Gets a smart contract transaction receipt. Receipts contain information about how a smart contract transaction was executed.
        /// This includes the value returned from a smart contract call and how much gas was used.  
        /// </summary>
        /// 
        /// <param name="txHash">A hash of the smart contract transaction (the transaction ID).</param>
        /// 
        /// <returns>The receipt for the smart contract.</returns> 
        [Route("receipt")]
        [HttpGet]
        public IActionResult GetReceipt([FromQuery] string txHash)
        {
            uint256 txHashNum = new uint256(txHash);
            Receipt receipt = this.receiptRepository.Retrieve(txHashNum);

            if (receipt == null)
            {
                this.logger.LogTrace("(-)[RECEIPT_NOT_FOUND]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    "The receipt was not found.",
                    "No stored transaction could be found for the supplied hash.");
            }

            var receiptResponse = new ReceiptResponse(receipt, this.network);

            return this.Json(receiptResponse);
        }

        // Note: We may not know exactly how to best structure "receipt search" queries until we start building 
        // a web3-like library. For now the following method serves as a very basic example of how we can query the block
        // bloom filters to retrieve events.


        /// <summary>
        /// Searches a smart contract's receipts for those which match a specific event. The SmartContract.Log() function
        /// is capable of storing C# structs, and structs are used to store information about different events occurring 
        /// on the smart contract. For example, a "TransferLog" struct could contain "From" and "To" fields and be used to log
        /// when a smart contract makes a transfer of funds from one wallet to another. The log entries are held inside the smart contract,
        /// indexed using the name of the struct, and are linked to individual transaction receipts.
        /// Therefore, it is possible to return a smart contract's transaction receipts
        /// which match a specific event (as defined by the struct name).  
        /// </summary>
        /// 
        /// <param name="contractAddress">The address of the smart contract to retrieve the receipts for.</param>
        /// <param name="eventName">The name of the event struct to retrieve matching receipts for.</param>
        /// 
        /// <returns>A list of receipts for transactions relating to a specific smart contract and a specific event in that smart contract.</returns>
        [Route("receipt-search")]
        [HttpGet]
        public async Task<IActionResult> ReceiptSearch([FromQuery] string contractAddress, [FromQuery] string eventName)
        {
            // Build the bytes we can use to check for this event.
            uint160 addressUint160 = contractAddress.ToUint160(this.network);
            byte[] addressBytes = addressUint160.ToBytes();
            byte[] eventBytes = Encoding.UTF8.GetBytes(eventName);

            // Loop through all headers and check bloom.
            IEnumerable<ChainedHeader> blockHeaders = this.chainIndexer.EnumerateToTip(this.chainIndexer.Genesis);
            List<ChainedHeader> matches = new List<ChainedHeader>();
            foreach(ChainedHeader chainedHeader in blockHeaders)
            {
                var scHeader = (ISmartContractBlockHeader) chainedHeader.Header;
                if (scHeader.LogsBloom.Test(addressBytes) && scHeader.LogsBloom.Test(eventBytes)) // TODO: This is really inefficient, should build bloom for query and then compare.
                    matches.Add(chainedHeader);
            }

            // For all matching headers, get the block from local db.
            List<NBitcoin.Block> blocks = new List<NBitcoin.Block>();
            foreach(ChainedHeader chainedHeader in matches)
            {
                blocks.Add(this.blockStore.GetBlock(chainedHeader.HashBlock));
            }

            // For each block, get all receipts, and if they match, add to list to return.
            List<ReceiptResponse> receiptResponses = new List<ReceiptResponse>();
            foreach(NBitcoin.Block block in blocks)
            {
                foreach(Transaction transaction in block.Transactions)
                {
                    Receipt storedReceipt = this.receiptRepository.Retrieve(transaction.GetHash());
                    if (storedReceipt == null) // not a smart contract transaction. Move to next transaction.
                        continue;

                    // Check if address and first topic (event name) match.
                    if (storedReceipt.Logs.Any(x => x.Address == addressUint160 && Enumerable.SequenceEqual(x.Topics[0], eventBytes)))
                        receiptResponses.Add(new ReceiptResponse(storedReceipt, this.network));
                }
            }

            return this.Json(receiptResponses);
        }

        /// <summary>
        /// Builds a transaction to create a smart contract. Although the transaction is created, the smart contract is not
        /// deployed on the network, and no gas or fees are consumed.
        /// Instead the created transaction is returned as a hexadecimal string within a JSON object.
        /// Transactions built using this method can be deployed using /api/SmartContractWallet/send-transaction.
        /// However, unless there is a need to closely examine the transaction before deploying it, you should use
        /// api/SmartContracts/build-and-send-create.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to build the transaction.</param>
        /// 
        /// <returns>A transaction ready to create a smart contract.</returns>
        [Route("build-create")]
        [HttpPost]
        public IActionResult BuildCreateSmartContractTransaction([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCreateContractTransactionResponse response = this.smartContractTransactionService.BuildCreateTx(request);

            if (!response.Success)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, response.Message, string.Empty);

            return this.Json(response);
        }

        /// <summary>
        /// Builds a transaction to call a smart contract method. Although the transaction is created, the
        /// call is not made, and no gas or fees are consumed.
        /// Instead the created transaction is returned as a JSON object.
        /// Transactions built using this method can be deployed using /api/SmartContractWallet/send-transaction
        /// However, unless there is a need to closely examine the transaction before deploying it, you should use
        /// api/SmartContracts/build-and-send-call.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to build the transaction.</param>
        /// 
        /// <returns>A transaction ready to call a method on a smart contract.</returns>
        [Route("build-call")]
        [HttpPost]
        public IActionResult BuildCallSmartContractTransaction([FromBody] BuildCallContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            var response = this.smartContractTransactionService.BuildCallTx(request);

            if (!response.Success)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, response.Message, string.Empty);

            return this.Json(response);
        }

        /// <summary>
        /// Builds a transaction to transfer funds on a smart contract network.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to build the transaction.</param>
        /// 
        /// <returns>The build transaction hex.</returns>
        [Route("build-transaction")]
        [HttpPost]
        public IActionResult BuildTransaction([FromBody] BuildContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            try
            {
                BuildContractTransactionResult result = this.smartContractTransactionService.BuildTx(request);

                return this.Json(result.Response);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a fee estimate for a specific smart contract account-based transfer transaction.
        /// This differs from fee estimation on standard networks due to the way inputs must be selected for account-based transfers.
        /// </summary>
        /// <param name="request">An object containing the parameters used to build the the fee estimation transaction.</param>
        /// <returns>The estimated fee for the transaction.</returns>
        [Route("estimate-fee")]
        [HttpPost]
        public IActionResult EstimateFee([FromBody] ScTxFeeEstimateRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            try
            {
                EstimateFeeResult result = this.smartContractTransactionService.EstimateFee(request);

                return this.Json(result.Fee);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Builds a transaction to create a smart contract and then broadcasts the transaction to the network.
        /// If the deployment is successful, methods on the smart contract can be subsequently called.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to build the transaction.</param>
        /// 
        /// <returns>The transaction used to create the smart contract. The result of the transaction broadcast is not returned
        /// and you should check for a transaction receipt to see if it was successful.</returns>
        [Route("build-and-send-create")]
        [HttpPost]
        public IActionResult BuildAndSendCreateSmartContractTransaction([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCreateContractTransactionResponse response = this.smartContractTransactionService.BuildCreateTx(request);

            if (!response.Success)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, response.Message, string.Empty);

            Transaction transaction = this.network.CreateTransaction(response.Hex);

            if (!this.connectionManager.ConnectedPeers.Any())
            {
                this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Can't send transaction: sending transaction requires at least one connection!", string.Empty);
            }

            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            // Check if transaction was actually added to a mempool.
            TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

            if (transactionBroadCastEntry?.State == Features.Wallet.Broadcasting.State.CantBroadcast)
            {
                this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
            }

            return this.Json(response);
        }

        /// <summary>
        /// Builds a transaction to call a smart contract method and then broadcasts the transaction to the network.
        /// If the call is successful, any changes to the smart contract balance or persistent data are propagated
        /// across the network.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to build the transaction.</param>
        ///
        /// <returns>The transaction used to call a smart contract method. The result of the transaction broadcast is not returned
        /// and you should check for a transaction receipt to see if it was successful.</returns>
        [Route("build-and-send-call")]
        [HttpPost]
        public IActionResult BuildAndSendCallSmartContractTransaction([FromBody] BuildCallContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCallContractTransactionResponse response = this.smartContractTransactionService.BuildCallTx(request);
            if (!response.Success)
                return this.Json(response);

            Transaction transaction = this.network.CreateTransaction(response.Hex);

            if (!this.connectionManager.ConnectedPeers.Any())
            {
                this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Can't send transaction: sending transaction requires at least one connection!", string.Empty);
            }

            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            // Check if transaction was actually added to a mempool.
            TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

            if (transactionBroadCastEntry?.State == Features.Wallet.Broadcasting.State.CantBroadcast)
            {
                this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
            }

            return this.Json(response);
        }

        /// <summary>
        /// Makes a local call to a method on a smart contract that has been successfully deployed. A transaction 
        /// is not created as the call is never propagated across the network. All persistent data held by the   
        /// smart contract is copied before the call is made. Only this copy is altered by the call
        /// and the actual data is unaffected. Even if an amount of funds are specified to send with the call,
        /// no funds are in fact sent.
        /// The purpose of this function is to query and test methods. 
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to build the transaction.</param>
        /// 
        /// <results>The result of the local call to the smart contract method.</results>
        [Route("local-call")]
        [HttpPost]
        public IActionResult LocalCallSmartContractTransaction([FromBody] LocalCallContractRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            // Rewrite the method name to a property name
            this.RewritePropertyGetterName(request);

            try
            {
                ContractTxData txData = this.smartContractTransactionService.BuildLocalCallTxData(request);

                ILocalExecutionResult result = this.localExecutor.Execute(
                    (ulong)this.chainIndexer.Height,
                    request.Sender?.ToUint160(this.network) ?? new uint160(),
                    string.IsNullOrWhiteSpace(request.Amount) ? (Money) request.Amount : 0,
                    txData);

                return this.Json(result, new JsonSerializerSettings
                {
                    ContractResolver = new ContractParametersContractResolver(this.network)
                });
            }
            catch (MethodParameterStringSerializerException e)
            {
                return this.Json(ErrorHelpers.BuildErrorResponse(HttpStatusCode.InternalServerError, e.Message,
                    "Error deserializing method parameters"));
            }
        }

        /// <summary>
        /// If the call is to a property, rewrites the method name to the getter method's name.
        /// </summary>
        private void RewritePropertyGetterName(LocalCallContractRequest request)
        {
            // Don't rewrite if there are params
            if (request.Parameters != null && request.Parameters.Any())
                return;

            byte[] contractCode = this.stateRoot.GetCode(request.ContractAddress.ToUint160(this.network));

            string contractType = this.stateRoot.GetContractType(request.ContractAddress.ToUint160(this.network));

            Result<IContractModuleDefinition> readResult = ContractDecompiler.GetModuleDefinition(contractCode);

            if (readResult.IsSuccess)
            {
                IContractModuleDefinition contractModule = readResult.Value;
                string propertyGetterName = contractModule.GetPropertyGetterMethodName(contractType, request.MethodName);

                if (propertyGetterName != null)
                {
                    request.MethodName = propertyGetterName;
                }
            }
        }

        /// <summary>
        /// Gets all addresses owned by a wallet which have a balance associated with them. This
        /// method effectively returns the balance of all the UTXOs associated with a wallet.
        /// In a case where multiple UTXOs are associated with one address, the amounts
        /// are tallied to give a total for that address.
        /// </summary>
        ///
        /// <param name="walletName">The name of the wallet to retrieve the addresses from.</param>
        /// 
        /// <returns>The addresses owned by a wallet which have a balance associated with them.</returns>
        [Route("address-balances")]
        [HttpGet]
        public IActionResult GetAddressesWithBalances([FromQuery] string walletName)
        {
            IEnumerable<IGrouping<HdAddress, UnspentOutputReference>> allSpendable = this.walletManager.GetSpendableTransactionsInWallet(walletName, MinConfirmationsAllChecks).GroupBy(x => x.Address);
            var result = new List<object>();
            foreach (IGrouping<HdAddress, UnspentOutputReference> grouping in allSpendable)
            {
                result.Add(new
                {
                    grouping.Key.Address,
                    Sum = grouping.Sum(x => x.Transaction.GetUnspentAmount(false))
                });
            }
            
            return this.Json(result);
        }

        private object InterpretStorageValue(MethodParameterDataType dataType, byte[] bytes)
        {
            switch (dataType)
            {
                case MethodParameterDataType.Bool:
                    return this.serializer.ToBool(bytes);
                case MethodParameterDataType.Byte:
                    return bytes[0];
                case MethodParameterDataType.Char:
                    return this.serializer.ToChar(bytes);
                case MethodParameterDataType.String:
                    return this.serializer.ToString(bytes);
                case MethodParameterDataType.UInt:
                    return this.serializer.ToUInt32(bytes);
                case MethodParameterDataType.Int:
                    return this.serializer.ToInt32(bytes);
                case MethodParameterDataType.ULong:
                    return this.serializer.ToUInt64(bytes);
                case MethodParameterDataType.Long:
                    return this.serializer.ToInt64(bytes);
                case MethodParameterDataType.Address:
                    return this.serializer.ToAddress(bytes);
                case MethodParameterDataType.ByteArray:
                    return bytes.ToHexString();
            }

            return null;
        }
    }
}