using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

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
        private readonly IBlockStoreCache blockStoreCache;
        private readonly CoinType coinType;
        private readonly ConcurrentChain chain;
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IContractStateRoot stateRoot;
        private readonly IWalletManager walletManager;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly IAddressGenerator addressGenerator;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;
        private readonly IReceiptRepository receiptRepository;

        public SmartContractsController(IBroadcasterManager broadcasterManager,
            IBlockStoreCache blockStoreCache,
            ConcurrentChain chain,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            IContractStateRoot stateRoot,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IAddressGenerator addressGenerator,
            IContractPrimitiveSerializer contractPrimitiveSerializer,
            IReceiptRepository receiptRepository)
        {
            this.stateRoot = stateRoot;
            this.walletTransactionHandler = walletTransactionHandler;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.blockStoreCache = blockStoreCache;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
            this.addressGenerator = addressGenerator;
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
            this.receiptRepository = receiptRepository;
        }

        [Route("code")]
        [HttpGet]
        public IActionResult GetCode([FromQuery]string address)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(address), address);

            uint160 addressNumeric = new Address(address).ToUint160(this.network);
            byte[] contractCode = this.stateRoot.GetCode(addressNumeric);

            if (contractCode == null || !contractCode.Any())
            {
                return Json(new GetCodeResponse
                {
                    Message = string.Format("No contract execution code exists at {0}", address)
                });
            }

            using (var memStream = new MemoryStream(contractCode))
            {
                var modDefinition = ModuleDefinition.ReadModule(memStream);
                var decompiler = new CSharpDecompiler(modDefinition, new DecompilerSettings { });
                // TODO: Update decompiler to display all code, not just this rando FirstOrDefault (given we now allow multiple types)
                string cSharp = decompiler.DecompileAsString(modDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>"));

                this.logger.LogTrace("(-)");

                return Json(new GetCodeResponse
                {
                    Message = string.Format("Contract execution code retrieved at {0}", address),
                    Bytecode = contractCode.ToHexString(),
                    CSharp = cSharp
                });
            }
        }

        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance([FromQuery]string address)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(address), address);

            uint160 addressNumeric = new Address(address).ToUint160(this.network);
            ulong balance = this.stateRoot.GetCurrentBalance(addressNumeric);

            this.logger.LogTrace("(-)");

            return Json(balance);
        }

        [Route("storage")]
        [HttpGet]
        public IActionResult GetStorage([FromQuery] GetStorageRequest request)
        {
            this.logger.LogTrace("(){0}:{1},{2}:{3},{4}:{5}", nameof(request.ContractAddress), request.ContractAddress, nameof(request.DataType), request.DataType, nameof(request.StorageKey), request.StorageKey);

            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODELSTATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            uint160 addressNumeric = new Address(request.ContractAddress).ToUint160(this.network);
            byte[] storageValue = this.stateRoot.GetStorageValue(addressNumeric, Encoding.UTF8.GetBytes(request.StorageKey));
            this.logger.LogTrace("(-){0}:{1}", nameof(storageValue), storageValue);

            return Json(GetStorageValue(request.DataType, storageValue).ToString());
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

            return Json(receiptResponse);
        }

        // Note: We may not know exactly how to best structure "receipt search" queries until we start building 
        // a web3-like library. For now the following method serves as a very basic example of how we can query the block
        // bloom filters to retrieve events.

        [Route("receipt-search")]
        [HttpGet]
        public async Task<IActionResult> ReceiptSearch([FromQuery] string contractAddress, [FromQuery] string eventName)
        {
            // Build the bytes we can use to check for this event.
            uint160 addressUint160 = new Address(contractAddress).ToUint160(this.network);
            byte[] addressBytes = addressUint160.ToBytes();
            byte[] eventBytes = Encoding.UTF8.GetBytes(eventName);

            // Loop through all headers and check bloom.
            IEnumerable<ChainedHeader> blockHeaders = this.chain.EnumerateToTip(this.chain.Genesis);
            List<ChainedHeader> matches = new List<ChainedHeader>();
            foreach(ChainedHeader chainedHeader in blockHeaders)
            {
                var scHeader = (SmartContractBlockHeader) chainedHeader.Header;
                if (scHeader.LogsBloom.Test(addressBytes) && scHeader.LogsBloom.Test(eventBytes)) // TODO: This is really inefficient, should build bloom for query and then compare.
                    matches.Add(chainedHeader);
            }

            // For all matching headers, get the block from local db.
            List<NBitcoin.Block> blocks = new List<NBitcoin.Block>();
            foreach(ChainedHeader chainedHeader in matches)
            {
                blocks.Add(await this.blockStoreCache.GetBlockAsync(chainedHeader.HashBlock).ConfigureAwait(false));
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

            return Json(receiptResponses);
        } 

        [Route("build-create")]
        [HttpPost]
        public IActionResult BuildCreateSmartContractTransaction([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            return Json(BuildCreateTx(request));
        }

        [Route("build-call")]
        [HttpPost]
        public IActionResult BuildCallSmartContractTransaction([FromBody] BuildCallContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            return Json(BuildCallTx(request));
        }


        [Route("build-and-send-create")]
        [HttpPost]
        public IActionResult BuildAndSendCreateSmartContractTransaction([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCreateContractTransactionResponse response = BuildCreateTx(request);
            if (!response.Success)
                return Json(response);

            Transaction transaction = this.network.CreateTransaction(response.Hex);
            this.walletManager.ProcessTransaction(transaction, null, null, false);
            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            return Json(response);
        }

        [Route("build-and-send-call")]
        [HttpPost]
        public IActionResult BuildAndSendCallSmartContractTransaction([FromBody] BuildCallContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCallContractTransactionResponse response = BuildCallTx(request);
            if (!response.Success)
                return Json(response);

            Transaction transaction = this.network.CreateTransaction(response.Hex);
            this.walletManager.ProcessTransaction(transaction, null, null, false);
            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            return Json(response);
        }

        [Route("address-balances")]
        [HttpGet]
        public IActionResult GetAddressesWithBalances([FromQuery] string walletName)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(walletName), walletName);

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

            this.logger.LogTrace("(-)");

            return Json(result);
        }

        private BuildCreateContractTransactionResponse BuildCreateTx(BuildCreateContractTransactionRequest request)
        {
            this.logger.LogTrace(request.ToString());

            AddressBalance addressBalance = this.walletManager.GetAddressBalance(request.Sender);
            if (addressBalance.AmountConfirmed == 0)
                return BuildCreateContractTransactionResponse.Failed($"The 'Sender' address you're trying to spend from doesn't have a confirmed balance. Current unconfirmed balance: {addressBalance.AmountUnconfirmed}. Please check the 'Sender' address.");

            var selectedInputs = new List<OutPoint>();
            selectedInputs = this.walletManager.GetSpendableTransactionsInWallet(request.WalletName, MinConfirmationsAllChecks).Where(x => x.Address.Address == request.Sender).Select(x => x.ToOutPoint()).ToList();

            ulong gasPrice = ulong.Parse(request.GasPrice);
            ulong gasLimit = ulong.Parse(request.GasLimit);

            SmartContractCarrier carrier;
            if (request.Parameters != null && request.Parameters.Any())
                carrier = SmartContractCarrier.CreateContract(ReflectionVirtualMachine.VmVersion, request.ContractCode.HexToByteArray(), gasPrice, new Gas(gasLimit), request.Parameters);
            else
                carrier = SmartContractCarrier.CreateContract(ReflectionVirtualMachine.VmVersion, request.ContractCode.HexToByteArray(), gasPrice, new Gas(gasLimit));

            HdAddress senderAddress = null;
            if (!string.IsNullOrWhiteSpace(request.Sender))
            {
                Features.Wallet.Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                HdAccount account = wallet.GetAccountByCoinType(request.AccountName, this.coinType);
                senderAddress = account.GetCombinedAddresses().FirstOrDefault(x => x.Address == request.Sender);
            }

            ulong totalFee = (gasPrice * gasLimit) + Money.Parse(request.FeeAmount);
            var walletAccountReference = new WalletAccountReference(request.WalletName, request.AccountName);
            var recipient = new Recipient { Amount = request.Amount ?? "0", ScriptPubKey = new Script(carrier.Serialize()) };
            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = walletAccountReference,
                TransactionFee = totalFee,
                ChangeAddress = senderAddress,
                SelectedInputs = selectedInputs,
                MinConfirmations = MinConfirmationsAllChecks,
                WalletPassword = request.Password,
                Recipients = new[] { recipient }.ToList()
            };

            try
            {
                Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);
                uint160 contractAddress = this.addressGenerator.GenerateAddress(transaction.GetHash(), 0);
                return BuildCreateContractTransactionResponse.Succeeded(transaction, context.TransactionFee, contractAddress.ToAddress(this.network));
            }
            catch (Exception exception)
            {
                return BuildCreateContractTransactionResponse.Failed(exception.Message);
            }
        }

        private BuildCallContractTransactionResponse BuildCallTx(BuildCallContractTransactionRequest request)
        {
            this.logger.LogTrace(request.ToString());

            AddressBalance addressBalance = this.walletManager.GetAddressBalance(request.Sender);
            if (addressBalance.AmountConfirmed == 0)
                return BuildCallContractTransactionResponse.Failed($"The 'Sender' address you're trying to spend from doesn't have a confirmed balance. Current unconfirmed balance: {addressBalance.AmountUnconfirmed}. Please check the 'Sender' address.");

            var selectedInputs = new List<OutPoint>();
            selectedInputs = this.walletManager.GetSpendableTransactionsInWallet(request.WalletName, MinConfirmationsAllChecks).Where(x => x.Address.Address == request.Sender).Select(x => x.ToOutPoint()).ToList();

            ulong gasPrice = ulong.Parse(request.GasPrice);
            ulong gasLimit = ulong.Parse(request.GasLimit);
            uint160 addressNumeric = new Address(request.ContractAddress).ToUint160(this.network);

            SmartContractCarrier carrier;
            if (request.Parameters != null && request.Parameters.Any())
                carrier = SmartContractCarrier.CallContract(ReflectionVirtualMachine.VmVersion, addressNumeric, request.MethodName, gasPrice, new Gas(gasLimit), request.Parameters);
            else
                carrier = SmartContractCarrier.CallContract(ReflectionVirtualMachine.VmVersion, addressNumeric, request.MethodName, gasPrice, new Gas(gasLimit));

            HdAddress senderAddress = null;
            if (!string.IsNullOrWhiteSpace(request.Sender))
            {
                Features.Wallet.Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                HdAccount account = wallet.GetAccountByCoinType(request.AccountName, this.coinType);
                senderAddress = account.GetCombinedAddresses().FirstOrDefault(x => x.Address == request.Sender);
            }

            ulong totalFee = (gasPrice * gasLimit) + Money.Parse(request.FeeAmount);
            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                TransactionFee = totalFee,
                ChangeAddress = senderAddress,
                SelectedInputs = selectedInputs,
                MinConfirmations = MinConfirmationsAllChecks,
                WalletPassword = request.Password,
                Recipients = new[] { new Recipient { Amount = request.Amount, ScriptPubKey = new Script(carrier.Serialize()) } }.ToList()
            };

            try
            {
                Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);
                return BuildCallContractTransactionResponse.Succeeded(request.MethodName, transaction, context.TransactionFee);
            }
            catch (Exception exception)
            {
                return BuildCallContractTransactionResponse.Failed(exception.Message);
            }
        }

        private object GetStorageValue(SmartContractDataType dataType, byte[] bytes)
        {
            switch (dataType)
            {
                case SmartContractDataType.Address:
                    return this.contractPrimitiveSerializer.Deserialize<Address>(bytes);
                case SmartContractDataType.Bool:
                    return this.contractPrimitiveSerializer.Deserialize<bool>(bytes);
                case SmartContractDataType.Bytes:
                    return this.contractPrimitiveSerializer.Deserialize<byte[]>(bytes);
                case SmartContractDataType.Char:
                    return this.contractPrimitiveSerializer.Deserialize<char>(bytes);
                case SmartContractDataType.Int:
                    return this.contractPrimitiveSerializer.Deserialize<int>(bytes);
                case SmartContractDataType.Long:
                    return this.contractPrimitiveSerializer.Deserialize<long>(bytes);
                case SmartContractDataType.Sbyte:
                    return this.contractPrimitiveSerializer.Deserialize<sbyte>(bytes);
                case SmartContractDataType.String:
                    return this.contractPrimitiveSerializer.Deserialize<string>(bytes);
                case SmartContractDataType.Uint:
                    return this.contractPrimitiveSerializer.Deserialize<uint>(bytes);
                case SmartContractDataType.Ulong:
                    return this.contractPrimitiveSerializer.Deserialize<ulong>(bytes);
            }
            return null;
        }
    }
}