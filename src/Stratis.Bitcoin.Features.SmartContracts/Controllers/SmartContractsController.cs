using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Serialization;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Controllers
{
    [Route("api/[controller]")]
    public class SmartContractsController : Controller
    {
        private readonly ContractStateRepositoryRoot stateRoot;
        private readonly IConsensusLoop consensus;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger logger;
        private readonly Network network;

        public SmartContractsController(ContractStateRepositoryRoot stateRoot, IConsensusLoop consensus, IWalletTransactionHandler walletTransactionHandler, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, Network network)
        {
            this.stateRoot = stateRoot;
            this.consensus = consensus;
            this.walletTransactionHandler = walletTransactionHandler;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
        }

        [Route("code")]
        [HttpGet]
        public IActionResult GetCode([FromQuery]string address)
        {
            uint160 addressNumeric = new Address(address).ToUint160(this.network);
            byte[] contractCode = this.stateRoot.GetCode(addressNumeric);

            // In the future, we could be more explicit about whether a contract exists (or indeed, did ever exist)
            if (contractCode == null || !contractCode.Any())
            {
                return Json(new GetCodeResponse
                {
                    CSharp = "",
                    Bytecode = ""
                });
            }

            using (var memStream = new MemoryStream(contractCode))
            {
                var modDefinition = ModuleDefinition.ReadModule(memStream);
                var decompiler = new CSharpDecompiler(modDefinition, new DecompilerSettings { });
                string cSharp = decompiler.DecompileAsString(modDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>"));
                return Json(new GetCodeResponse
                {
                    CSharp = cSharp,
                    Bytecode = contractCode.ToHexString()
                });
            }
        }

        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance([FromQuery]string address)
        {
            uint160 addressNumeric = new Address(address).ToUint160(this.network);
            ulong balance = this.stateRoot.GetCurrentBalance(addressNumeric);
            return Json(balance);
        }

        [Route("storage")]
        [HttpGet]
        public IActionResult GetStorage([FromQuery] GetStorageRequest request)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            uint160 addressNumeric = new Address(request.ContractAddress).ToUint160(this.network);
            byte[] storageValue = this.stateRoot.GetStorageValue(addressNumeric, Encoding.UTF8.GetBytes(request.StorageKey));
            return Json(GetStorageValue(request.DataType, storageValue).ToString());
        }

        [Route("build-create")]
        [HttpPost]
        public IActionResult BuildCreateSmartContractTransaction([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            ulong gasPrice = ulong.Parse(request.GasPrice);
            ulong gasLimit = ulong.Parse(request.GasLimit);

            SmartContractCarrier carrier;
            if (request.Parameters != null && request.Parameters.Any())
            {
                carrier = SmartContractCarrier.CreateContract(ReflectionVirtualMachine.VmVersion, request.ContractCode.HexToByteArray(), gasPrice, new Gas(gasLimit), request.Parameters);
            }
            else
            {
                carrier = SmartContractCarrier.CreateContract(ReflectionVirtualMachine.VmVersion, request.ContractCode.HexToByteArray(), gasPrice, new Gas(gasLimit));
            }

            ulong totalFee = gasPrice * gasLimit + ulong.Parse(request.FeeAmount);
            var context = new TransactionBuildContext(
                new WalletAccountReference(request.WalletName, request.AccountName),
                new[] { new Recipient { Amount = request.Amount, ScriptPubKey = new Script(carrier.Serialize()) } }.ToList(),
                request.Password)
            {
                TransactionFee = totalFee,
            };

            Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

            var model = new BuildCreateContractTransactionResponse
            {
                Hex = transactionResult.ToHex(),
                Fee = context.TransactionFee,
                TransactionId = transactionResult.GetHash(),
                NewContractAddress = transactionResult.GetNewContractAddress().ToAddress(this.network)
            };

            return this.Json(model);
        }

        [Route("build-call")]
        [HttpPost]
        public IActionResult BuildCallSmartContractTransaction([FromBody] BuildCallContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            ulong gasPrice = ulong.Parse(request.GasPrice);
            ulong gasLimit = ulong.Parse(request.GasLimit);
            uint160 addressNumeric = new Address(request.ContractAddress).ToUint160(this.network);

            SmartContractCarrier carrier;
            if (request.Parameters != null && request.Parameters.Any())
            {
                carrier = SmartContractCarrier.CallContract(ReflectionVirtualMachine.VmVersion, addressNumeric, request.MethodName, gasPrice, new Gas(gasLimit), request.Parameters);
            }
            else
            {
                carrier = SmartContractCarrier.CallContract(ReflectionVirtualMachine.VmVersion, addressNumeric, request.MethodName, gasPrice, new Gas(gasLimit));
            }

            ulong totalFee = gasPrice * gasLimit + ulong.Parse(request.FeeAmount);
            var context = new TransactionBuildContext(
                new WalletAccountReference(request.WalletName, request.AccountName),
                new[] { new Recipient { Amount = request.Amount, ScriptPubKey = new Script(carrier.Serialize()) } }.ToList(),
                request.Password)
            {
                TransactionFee = totalFee,
            };

            Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

            var model = new BuildCallContractTransactionResponse
            {
                Hex = transactionResult.ToHex(),
                Fee = context.TransactionFee,
                TransactionId = transactionResult.GetHash()
            };

            return this.Json(model);
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

        /// <summary>
        /// Builds an <see cref="IActionResult"/> containing errors contained in the <see cref="ControllerBase.ModelState"/>.
        /// </summary>
        /// <returns>A result containing the errors.</returns>
        private static IActionResult BuildErrorResponse(ModelStateDictionary modelState)
        {
            List<ModelError> errors = modelState.Values.SelectMany(e => e.Errors).ToList();
            return ErrorHelpers.BuildErrorResponse(
                HttpStatusCode.BadRequest,
                string.Join(Environment.NewLine, errors.Select(m => m.ErrorMessage)),
                string.Join(Environment.NewLine, errors.Select(m => m.Exception?.Message)));
        }
    }
}
