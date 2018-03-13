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
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;

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

        public SmartContractsController(ContractStateRepositoryRoot stateRoot, IConsensusLoop consensus, IWalletTransactionHandler walletTransactionHandler, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.stateRoot = stateRoot;
            this.consensus = consensus;
            this.walletTransactionHandler = walletTransactionHandler;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }
        
        [Route("code")]
        [HttpGet]
        public IActionResult GetCode([FromQuery]string address)
        {
            var numeric = new uint160(address);
            byte[] contractCode = GetSyncedStateRoot().GetCode(numeric);

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
            var numeric = new uint160(address);
            ulong balance = GetSyncedStateRoot().GetCurrentBalance(numeric);
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

            uint160 numeric = new uint160(request.ContractAddress);
            byte[] storageValue = GetSyncedStateRoot().GetStorageValue(numeric, Encoding.UTF8.GetBytes(request.StorageKey));
            if (SmartContractCarrierDataType.UInt == request.DataType)
                return Json(BitConverter.ToUInt32(storageValue, 0));
            return Json(Encoding.UTF8.GetString(storageValue)); // TODO: Use the modular serializer Francois is working on :)
        }

        [Route("build-create")]
        [HttpPost]
        public IActionResult BuildCreateSmartContractTransaction([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            ulong airPrice = ulong.Parse(request.AirPrice);
            ulong airLimit = ulong.Parse(request.AirLimit);

            SmartContractCarrier carrier;
            if (request.Parameters != null && request.Parameters.Any())
            {
                carrier = SmartContractCarrier.CreateContract(ReflectionVirtualMachine.VmVersion, request.ContractCode.HexToByteArray(), airPrice, new Gas(airLimit), request.Parameters);
            }
            else
            {
                carrier = SmartContractCarrier.CreateContract(ReflectionVirtualMachine.VmVersion, request.ContractCode.HexToByteArray(), airPrice, new Gas(airLimit));
            }

            ulong totalFee = airPrice * airLimit + ulong.Parse(request.FeeAmount);
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
                NewContractAddress = transactionResult.GetNewContractAddress()
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

            ulong airPrice = ulong.Parse(request.AirPrice);
            ulong airLimit = ulong.Parse(request.AirLimit);

            SmartContractCarrier carrier;
            if(request.Parameters != null && request.Parameters.Any())
            {
                carrier = SmartContractCarrier.CallContract(ReflectionVirtualMachine.VmVersion, new uint160(request.ContractAddress), request.MethodName, airPrice, new Gas(airLimit), request.Parameters);
            }
            else
            {
                carrier = SmartContractCarrier.CallContract(ReflectionVirtualMachine.VmVersion, new uint160(request.ContractAddress), request.MethodName, airPrice, new Gas(airLimit));
            }

            ulong totalFee = airPrice * airLimit + ulong.Parse(request.FeeAmount);
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
                NewContractAddress = transactionResult.GetNewContractAddress()
            };

            return this.Json(model);
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

        private ContractStateRepositoryRoot GetSyncedStateRoot()
        {
            return this.stateRoot.GetSnapshotTo(this.consensus.Tip.Header.HashStateRoot.ToBytes());
        }
    }
}
