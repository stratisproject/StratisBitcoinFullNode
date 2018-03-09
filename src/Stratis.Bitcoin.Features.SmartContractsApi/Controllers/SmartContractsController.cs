using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.SmartContractsApi.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;

namespace Stratis.Bitcoin.Features.SmartContractsApi.Controllers
{
    [Route("api/[controller]")]
    public class SmartContractsController : Controller
    {
        private readonly IContractStateRepository stateRoot;
        private readonly IConsensusLoop consensus;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger logger;

        public SmartContractsController(IContractStateRepository stateRoot, IConsensusLoop consensus, IWalletTransactionHandler walletTransactionHandler, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
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
            uint160 numeric = new uint160(address);
            byte[] contractCode = this.stateRoot.GetCode(numeric);
            ModuleDefinition modDefinition = ModuleDefinition.ReadModule(new MemoryStream(contractCode));
            CSharpDecompiler decompiler = new CSharpDecompiler(modDefinition, new DecompilerSettings { });
            string cSharp = decompiler.DecompileAsString(modDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>"));
            return Json(new GetCodeResponse
            {
                CSharp = cSharp,
                Bytecode = contractCode.ToHexString()
            });
        }

        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance([FromQuery]string address)
        {
            uint160 numeric = new uint160(address);
            ulong balance = this.stateRoot.GetCurrentBalance(numeric);
            return Json(balance);
        }

        [Route("call-method")]
        [HttpGet]
        public IActionResult CallMethod([FromQuery] CallMethodRequest request)
        {
            throw new NotImplementedException();
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

            SmartContractCarrier carrier = SmartContractCarrier.CreateContract(ReflectionVirtualMachine.VmVersion, request.ContractCode.HexToByteArray(), airPrice, new Gas(airLimit), request.Parameters);
            ulong totalFee = airPrice * airLimit + ulong.Parse(request.FeeAmount);
            TransactionBuildContext context = new TransactionBuildContext(
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

            SmartContractCarrier carrier = SmartContractCarrier.CallContract(ReflectionVirtualMachine.VmVersion, new uint160(request.ContractAddress), request.MethodName, airPrice, new Gas(airLimit), request.Parameters);
            ulong totalFee = airPrice * airLimit + ulong.Parse(request.FeeAmount);
            TransactionBuildContext context = new TransactionBuildContext(
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
    }
}
