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
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
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
        private readonly ConcurrentChain chain;
        private readonly CSharpContractDecompiler contractDecompiler;
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly IWalletManager walletManager;
        private readonly ISerializer serializer;
        private readonly IReceiptRepository receiptRepository;
        private readonly ILocalExecutor localExecutor;
        private readonly ISmartContractTransactionService smartContractTransactionService;

        public SmartContractsController(IBroadcasterManager broadcasterManager,
            IBlockStore blockStore,
            ConcurrentChain chain,
            CSharpContractDecompiler contractDecompiler,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            IStateRepositoryRoot stateRoot,
            IWalletManager walletManager,
            ISerializer serializer,
            IReceiptRepository receiptRepository,
            ILocalExecutor localExecutor,
            ISmartContractTransactionService smartContractTransactionService)
        {
            this.stateRoot = stateRoot;
            this.contractDecompiler = contractDecompiler;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.chain = chain;
            this.blockStore = blockStore;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
            this.serializer = serializer;
            this.receiptRepository = receiptRepository;
            this.localExecutor = localExecutor;
            this.smartContractTransactionService = smartContractTransactionService;
        }

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

            Result<string> sourceResult = this.contractDecompiler.GetSource(contractCode);

            return this.Json(new GetCodeResponse
            {
                Message = string.Format("Contract execution code retrieved at {0}", address),
                Bytecode = contractCode.ToHexString(),
                CSharp = sourceResult.IsSuccess ? sourceResult.Value : sourceResult.Error // Show the source, or the reason why the source couldn't be retrieved.
            });
        }

        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance([FromQuery]string address)
        {
            uint160 addressNumeric = address.ToUint160(this.network);
            ulong balance = this.stateRoot.GetCurrentBalance(addressNumeric);
            Money moneyBalance = Money.Satoshis(balance);
            return this.Json(moneyBalance.ToString(false));
        }

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
                    "Receipt not found.",
                    "Could not find a stored transaction for this hash.");
            }

            var receiptResponse = new ReceiptResponse(receipt, this.network);

            return this.Json(receiptResponse);
        }

        // Note: We may not know exactly how to best structure "receipt search" queries until we start building 
        // a web3-like library. For now the following method serves as a very basic example of how we can query the block
        // bloom filters to retrieve events.

        [Route("receipt-search")]
        [HttpGet]
        public async Task<IActionResult> ReceiptSearch([FromQuery] string contractAddress, [FromQuery] string eventName)
        {
            // Build the bytes we can use to check for this event.
            uint160 addressUint160 = contractAddress.ToUint160(this.network);
            byte[] addressBytes = addressUint160.ToBytes();
            byte[] eventBytes = Encoding.UTF8.GetBytes(eventName);

            // Loop through all headers and check bloom.
            IEnumerable<ChainedHeader> blockHeaders = this.chain.EnumerateToTip(this.chain.Genesis);
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
                blocks.Add(await this.blockStore.GetBlockAsync(chainedHeader.HashBlock).ConfigureAwait(false));
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

        [Route("build-create")]
        [HttpPost]
        public IActionResult BuildCreateSmartContractTransaction([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            return this.Json(this.smartContractTransactionService.BuildCreateTx(request));
        }

        [Route("build-call")]
        [HttpPost]
        public IActionResult BuildCallSmartContractTransaction([FromBody] BuildCallContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            return this.Json(this.smartContractTransactionService.BuildCallTx(request));
        }


        [Route("build-and-send-create")]
        [HttpPost]
        public IActionResult BuildAndSendCreateSmartContractTransaction([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCreateContractTransactionResponse response = this.smartContractTransactionService.BuildCreateTx(request);

            if (!response.Success)
                return this.Json(response);

            Transaction transaction = this.network.CreateTransaction(response.Hex);
            this.walletManager.ProcessTransaction(transaction, null, null, false);
            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            return this.Json(response);
        }

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
            this.walletManager.ProcessTransaction(transaction, null, null, false);
            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            return this.Json(response);
        }

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
                    (ulong)this.chain.Height,
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
                    Sum = grouping.Sum(x => x.Transaction.SpendableAmount(false))
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