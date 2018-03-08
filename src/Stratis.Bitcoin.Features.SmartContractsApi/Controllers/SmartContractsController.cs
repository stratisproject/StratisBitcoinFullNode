using System.IO;
using System.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.SmartContractsApi.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;

namespace Stratis.Bitcoin.Features.SmartContractsApi.Controllers
{
    [Route("api/[controller]")]
    public class SmartContractsController : Controller
    {
        private readonly IContractStateRepository stateRoot;
        private readonly IConsensusLoop consensus;
        private readonly IWalletManager walletManager;
        private readonly IWalletSyncManager walletSyncManager;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger logger;

        public SmartContractsController(IContractStateRepository stateRoot, IConsensusLoop consensus, IWalletManager walletManager, IWalletSyncManager walletSyncManager, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.stateRoot = stateRoot;
            this.consensus = consensus;
            this.walletManager = walletManager;
            this.walletSyncManager = walletSyncManager;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }
        
        [Route("code")]
        [HttpGet]
        public IActionResult GetCode(string address)
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
        public IActionResult GetBalance(string address)
        {
            uint160 numeric = new uint160(address);
            ulong balance = this.stateRoot.GetCurrentBalance(numeric);
            return Json(balance);
        }

        //[Route("build-create")]
        //[HttpPost]
        //public IActionResult BuildCreateSmartContractTransaction()
        //{

        //}


        //[Route("storage")]
        //[HttpGet]
        //public IActionResult GetStorage(string address, string storageValue, )
        //{
        //    uint160 numeric = new uint160(address);

        //    byte[] contractCode = this.stateRoot.GetCode(numeric);
        //    ModuleDefinition modDefinition = ModuleDefinition.ReadModule(new MemoryStream(contractCode));
        //    CSharpDecompiler decompiler = new CSharpDecompiler(modDefinition, new DecompilerSettings { });
        //    string cSharp = decompiler.DecompileAsString(modDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>"));
        //    return Json(new GetCodeResponse
        //    {
        //        CSharp = cSharp,
        //        Bytecode = contractCode.ToHexString()
        //    });
        //}

    }
}
