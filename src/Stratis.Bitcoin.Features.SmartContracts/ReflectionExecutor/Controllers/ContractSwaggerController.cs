using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.Core.State;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    /// <summary>
    /// Controller for dynamically generating swagger documents for smart contract assemblies.
    /// </summary>
    [Route("swagger/contracts")]
    public class ContractSwaggerController : Controller
    {
        private readonly ILoader loader;
        private readonly IWalletManager walletmanager;
        private readonly IStateRepositoryRoot stateRepository;
        private readonly Network network;
        private readonly SwaggerGeneratorOptions options;
        private readonly JsonSerializer swaggerSerializer;

        public ContractSwaggerController(
            ILoader loader,
            IWalletManager walletmanager,
            IOptions<MvcJsonOptions> mvcJsonOptions,
            IOptions<SwaggerGeneratorOptions> options,
            IStateRepositoryRoot stateRepository,
            Network network)
        {
            this.loader = loader;
            this.walletmanager = walletmanager;
            this.stateRepository = stateRepository;
            this.network = network;
            this.options = options.Value;
            this.swaggerSerializer = SwaggerSerializerFactory.Create(mvcJsonOptions);
        }

        /// <summary>
        /// Dynamically generates a swagger document for the contract at the given address.
        /// </summary>
        /// <param name="address">The contract's address.</param>
        /// <returns>A <see cref="SwaggerDocument"/> model.</returns>
        /// <exception cref="Exception"></exception>
        [Route("{address}")]
        [HttpGet]
        [SwaggerOperation(description: "test")]
        public async Task<IActionResult> ContractSwaggerDoc(string address)
        {
            var code = this.stateRepository.GetCode(address.ToUint160(this.network));

            if (code == null)
                throw new Exception("Contract does not exist");

            Result<IContractAssembly> assemblyLoadResult = this.loader.Load((ContractByteCode) code);

            if (assemblyLoadResult.IsFailure)
                throw new Exception("Error loading assembly");

            IContractAssembly assembly = assemblyLoadResult.Value;

            // Default wallet is the first wallet as ordered by name.
            string defaultWalletName = this.walletmanager.GetWalletsNames().OrderBy(n => n).First();

            // Default address is the first address with a balance, or string.Empty if no addresses have been created.
            // Ordering this way is consistent with the wallet UI, ie. whatever appears first in the wallet will appear first here.
            string defaultAddress = this.walletmanager.GetAccountAddressesWithBalance(defaultWalletName).FirstOrDefault()?.Address ?? string.Empty;
            
            var swaggerGen = new ContractSwaggerDocGenerator(this.options, address, assembly, defaultWalletName, defaultAddress);

            SwaggerDocument doc = swaggerGen.GetSwagger("contracts");

            var jsonBuilder = new StringBuilder();

            using (var writer = new StringWriter(jsonBuilder))
            {
                this.swaggerSerializer.Serialize(writer, doc);
                var j = writer.ToString();
                return Ok(j);
            }
        }
    }
}
