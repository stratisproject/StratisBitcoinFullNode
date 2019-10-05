using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.Core.State;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    [Route("swagger/contracts")]
    public class ContractSwaggerController : Controller
    {
        private readonly ILoader loader;
        private readonly IStateRepositoryRoot stateRepository;
        private readonly Network network;
        private readonly SwaggerGeneratorOptions options;
        private readonly JsonSerializer swaggerSerializer;

        public ContractSwaggerController(
            ILoader loader,
            IOptions<MvcJsonOptions> mvcJsonOptions,
            IOptions<SwaggerGeneratorOptions> options,
            IStateRepositoryRoot stateRepository,
            Network network)
        {
            this.loader = loader;
            this.stateRepository = stateRepository;
            this.network = network;
            this.options = options.Value;
            this.swaggerSerializer = SwaggerSerializerFactory.Create(mvcJsonOptions);
        }

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

            var swaggerGen = new ContractSwaggerDocGenerator(this.options, address, assembly);

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
