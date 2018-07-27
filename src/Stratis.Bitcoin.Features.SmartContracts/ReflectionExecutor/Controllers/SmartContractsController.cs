using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
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
        private readonly CoinType coinType;
        private readonly ILogger logger;
        private readonly ISmartContractReceiptStorage receiptStorage;
        private readonly Network network;
        private readonly ContractStateRepositoryRoot stateRoot;
        private readonly IWalletManager walletManager;
        private readonly IWalletTransactionHandler walletTransactionHandler;

        public SmartContractsController(
            IBroadcasterManager broadcasterManager,
            IConsensusLoop consensus,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            ISmartContractReceiptStorage receiptStorage,
            ContractStateRepositoryRoot stateRoot,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler)
        {
            this.receiptStorage = receiptStorage;
            this.stateRoot = stateRoot;
            this.walletTransactionHandler = walletTransactionHandler;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
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
            this.logger.LogTrace("(){0}:{1}", nameof(txHash), txHash);
            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODELSTATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            uint256 txHashNum = new uint256(txHash);
            SmartContractReceipt receipt = this.receiptStorage.GetReceipt(txHashNum);

            if (receipt == null)
            {
                this.logger.LogTrace("(-)[RECEIPT_NOT_FOUND]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    "Receipt not found.",
                    "Could not find a stored transaction for this hash.");
            }

            return Json(ReceiptModel.FromSmartContractReceipt(receipt, this.network));
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
            var context = new TransactionBuildContext(this.network, walletAccountReference, new[] { recipient }.ToList(), request.Password)
            {
                TransactionFee = totalFee,
                ChangeAddress = senderAddress,
                SelectedInputs = selectedInputs,
                MinConfirmations = MinConfirmationsAllChecks,
            };

            try
            {
                Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);
                return BuildCreateContractTransactionResponse.Succeeded(transaction, context.TransactionFee, transaction.GetNewContractAddress().ToAddress(this.network));
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
            var context = new TransactionBuildContext(this.network,
                new WalletAccountReference(request.WalletName, request.AccountName),
                new[] { new Recipient { Amount = request.Amount, ScriptPubKey = new Script(carrier.Serialize()) } }.ToList(),
                request.Password)
            {
                TransactionFee = totalFee,
                ChangeAddress = senderAddress,
                SelectedInputs = selectedInputs,
                MinConfirmations = MinConfirmationsAllChecks,
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
            PersistentStateSerializer serializer = new PersistentStateSerializer();
            switch (dataType)
            {
                case SmartContractDataType.Address:
                    return serializer.Deserialize<Address>(bytes, this.network);
                case SmartContractDataType.Bool:
                    return serializer.Deserialize<bool>(bytes, this.network);
                case SmartContractDataType.Bytes:
                    return serializer.Deserialize<byte[]>(bytes, this.network);
                case SmartContractDataType.Char:
                    return serializer.Deserialize<char>(bytes, this.network);
                case SmartContractDataType.Int:
                    return serializer.Deserialize<int>(bytes, this.network);
                case SmartContractDataType.Long:
                    return serializer.Deserialize<long>(bytes, this.network);
                case SmartContractDataType.Sbyte:
                    return serializer.Deserialize<sbyte>(bytes, this.network);
                case SmartContractDataType.String:
                    return serializer.Deserialize<string>(bytes, this.network);
                case SmartContractDataType.Uint:
                    return serializer.Deserialize<uint>(bytes, this.network);
                case SmartContractDataType.Ulong:
                    return serializer.Deserialize<ulong>(bytes, this.network);
            }
            return null;
        }
    }
}