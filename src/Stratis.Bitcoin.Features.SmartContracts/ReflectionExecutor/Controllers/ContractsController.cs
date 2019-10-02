using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using NBitcoin;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.State;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    public class SmartContractPathFactory
    {
        private readonly IApiDescriptionGroupCollectionProvider apiDescriptionsProvider;

        private const string TransactionMethodInvocationPath = "api/SmartContractWallet/call";
        private const string LocalMethodInvocationPath = "api/SmartContracts/local-call";

        public SmartContractPathFactory(IApiDescriptionGroupCollectionProvider apiDescriptionsProvider)
        {
            this.apiDescriptionsProvider = apiDescriptionsProvider;
        }

        public PathItem CreatePathItem(MethodInfo method)
        {
            var path = new PathItem();

            path.Post = this.CreateOperation(method);
            
            return path;
        }

        private Operation CreateOperation(MethodInfo methodInfo)
        {
            // TODO get the api description for /api/SmartContractWallet/call
            var apiDescription = this.apiDescriptionsProvider.ApiDescriptionGroups.Items.SelectMany(items => items.Items);
            
            // Build the parameters based on the method params + some wallet model params with default
            // Have to build a controller with this endpoint, or some kind of dynamic invocation support?
            return new Operation
            {
                OperationId = methodInfo.Name,
                Tags = new List<string>{ methodInfo.Name },
                Consumes = new List<string> { "application/json-patch+json", "application/json", "text/json" },
                Produces = new List<string>(),
                //Parameters = CreateParameters(apiDescription, schemaRegistry),
                //Responses = CreateResponses(apiDescription, schemaRegistry),
            };
        }

        public PathItem CreatePathItem(PropertyInfo property)
        {
            return new PathItem();
        }
    }
    public class ContractSwaggerGenerator : ISwaggerProvider
    {
        private readonly SwaggerGeneratorOptions options;
        private readonly IApiDescriptionGroupCollectionProvider apiDescriptionsProvider;
        private readonly string base58Address;
        private readonly IStateRepository state;
        private readonly Network network;

        public ContractSwaggerGenerator(IApiDescriptionGroupCollectionProvider apiDescriptionsProvider, SwaggerGeneratorOptions options, string base58Address, IStateRepository state, Network network)
        {
            this.options = options ?? new SwaggerGeneratorOptions();
            this.apiDescriptionsProvider = apiDescriptionsProvider;
            this.base58Address = base58Address;
            this.state = state;
            this.network = network;
        }

        /// <summary>
        /// Reflects the methods of the contract and the parameters and builds the schemas and definitions for it.
        /// </summary>
        /// <param name="documentName"></param>
        /// <param name="host"></param>
        /// <param name="basePath"></param>
        /// <param name="schemes"></param>
        /// <returns></returns>
        /// <exception cref="UnknownSwaggerDocument"></exception>
        public SwaggerDocument GetSwagger(string documentName = "contract", string host = null, string basePath = null, string[] schemes = null)
        {
            if (!this.options.SwaggerDocs.TryGetValue(documentName, out Info info))
                throw new UnknownSwaggerDocument(documentName);

            info.Description = $"Contract API for {this.base58Address}";

            uint160 address = this.base58Address.ToUint160(this.network);

            byte[] contractCode = this.state.GetCode(address);

            var assembly = new ContractAssembly(contractCode);

            // Get the public methods of the contract
            // Get the public properties of the contract
            // Build the paths for the methods - transaction + local call
            // Build the paths for the properties - transaction + local call
            var swaggerDoc = new SwaggerDocument
            {
                Info = info,
                Host = host,
                BasePath = basePath,
                Schemes = schemes,
                Paths = this.CreatePathItems(assembly.GetPublicMethods(), null),
                //Definitions = schemaRegistry.Definitions,
                SecurityDefinitions = this.options.SecurityDefinitions.Any() ? this.options.SecurityDefinitions : null,
                Security = this.options.SecurityRequirements.Any() ? this.options.SecurityRequirements : null
            };

            return swaggerDoc;
            
        }

        private IDictionary<string, PathItem> CreatePathItems(IEnumerable<MethodInfo> publicMethods, IEnumerable<PropertyInfo> publicProperties)
        {
            var pathFactory = new SmartContractPathFactory(this.apiDescriptionsProvider);

            var result = new Dictionary<string, PathItem>();

            foreach (MethodInfo method in publicMethods)
            {
                result.Add(method.Name, pathFactory.CreatePathItem(method));
            }

            return result;
        }
    }

    [Route("swagger/[controller]")]
    public class ContractsController : Controller
    {
        private readonly ISwaggerProvider swaggerProvider;
        private readonly IApiDescriptionGroupCollectionProvider desc;
        private readonly IStateRepositoryRoot stateRepository;
        private readonly Network network;
        private readonly SwaggerGeneratorOptions options;
        private readonly SwaggerUIOptions uiOptions;

        public ContractsController(ISwaggerProvider swaggerProvider, IApiDescriptionGroupCollectionProvider desc, IOptions<SwaggerGeneratorOptions> options, IOptions<SwaggerUIOptions> uiOptions, IStateRepositoryRoot stateRepository, Network network)
        {
            this.swaggerProvider = swaggerProvider;
            this.desc = desc;
            this.stateRepository = stateRepository;
            this.network = network;
            this.options = options.Value;
            this.uiOptions = uiOptions.Value;
        }

        [Route("{address}")]
        [HttpGet]
        [SwaggerOperation(description: "test")]
        public IActionResult ContractSwaggerDoc(string address)
        {
            var sp = new ContractSwaggerGenerator(desc, this.options, address, this.stateRepository, this.network);
            var doc = sp.GetSwagger("contracts");
            return Ok(doc);
        }

    }
}
