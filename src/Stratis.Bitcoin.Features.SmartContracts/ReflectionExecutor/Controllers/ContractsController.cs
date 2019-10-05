using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class ControllerThatWillEventuallyBeDynamicallyGenerated : Controller
    {
        [Route("api/contract/{address}/method/{method}")]
        [HttpPost]
        public IActionResult CallMethod([FromRoute] string address, [FromRoute] string method)
        {
            string requestBody;
            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            // TODO map request body to JSON object, extract transaction-related params, build new request model, then call the regular SC controller.

            // Map parameters to our contract object and try to invoke it.
            // This will need to proxy to the actual SC controller

            return Ok(requestBody);
        }


        [Route("api/contract/{address}/property/{property}")]
        [HttpGet]
        public IActionResult LocalCallProperty([FromRoute] string address, [FromRoute] string property)
        {
            string requestBody;

            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            // TODO map to local call and return result.

            return Ok(requestBody);
        }
    }

    public class ContractSwaggerDocGenerator : ISwaggerProvider
    {
        private readonly string address;
        private readonly IContractAssembly assembly;
        private readonly SwaggerGeneratorOptions options;

        public ContractSwaggerDocGenerator(SwaggerGeneratorOptions options, string address, IContractAssembly assembly)
        {
            this.address = address;
            this.assembly = assembly;
            this.options = options;
        }

        public SwaggerDocument GetSwagger(string documentName, string host = null, string basePath = null, string[] schemes = null)
        {
            if (!this.options.SwaggerDocs.TryGetValue(documentName, out Info info))
                throw new UnknownSwaggerDocument(documentName);

            IDictionary<string, Schema> definitions = this.CreateDefinitions();

            info.Title = $"{this.assembly.GetDeployedType().Name} Contract API";
            info.Description = $"{this.address}";

            var swaggerDoc = new SwaggerDocument
            {
                Info = info,
                Host = host,
                BasePath = basePath,
                Schemes = schemes,
                Paths = this.CreatePathItems(definitions),
                Definitions = definitions,
                SecurityDefinitions = this.options.SecurityDefinitions.Any() ? this.options.SecurityDefinitions : null,
                Security = this.options.SecurityRequirements.Any() ? this.options.SecurityRequirements : null
            };

            return swaggerDoc;
        }

        private IDictionary<string, Schema> CreateDefinitions()
        {
            // Creates schema for each of the methods in the contract.
            var schemaFactory = new ContractSchemaFactory();

            return schemaFactory.Map(this.assembly);
        }

        private IDictionary<string, PathItem> CreatePathItems(IDictionary<string, Schema> schema)
        {
            // Creates path items for each of the methods & properties in the contract + their schema.

            IEnumerable<MethodInfo> methods = this.assembly.GetPublicMethods();

            var methodPaths = methods
                .ToDictionary(k => $"/api/contract/{this.address}/method/{k.Name}", v => this.CreatePathItem(v, schema));

            IEnumerable<PropertyInfo> properties = this.assembly.GetPublicGetterProperties();

            var propertyPaths = properties
                .ToDictionary(k => $"/api/contract/{this.address}/property/{k.Name}", v => this.CreatePathItem(v));
            
            foreach (KeyValuePair<string, PathItem> item in propertyPaths)
            {
                methodPaths[item.Key] = item.Value;
            }

            return methodPaths;
        }

        private PathItem CreatePathItem(PropertyInfo propertyInfo)
        {
            var pathItem = new PathItem();

            var operation = new Operation();

            operation.Tags = new[] { propertyInfo.Name };
            operation.OperationId = propertyInfo.Name; // TODO - Method name
            operation.Consumes = new[] { "application/json", "text/json", "application/*+json" };

            operation.Responses = new Dictionary<string, Response>
            {
                {"200", new Response {Description = "Success"}}
            };

            pathItem.Get = operation;

            return pathItem;
        }

        private PathItem CreatePathItem(MethodInfo methodInfo, IDictionary<string, Schema> schema)
        {
            var pathItem = new PathItem();

            var operation = new Operation();

            operation.Tags = new[] { methodInfo.Name };
            operation.OperationId = methodInfo.Name;
            operation.Consumes = new[] { "application/json", "text/json", "application/*+json" };

            var bodyParam = new BodyParameter
            {
                Name = methodInfo.Name,
                In = "body",
                Required = true,
                Schema = schema[methodInfo.Name]
            };

            operation.Parameters = new List<IParameter> { bodyParam };
            operation.Responses = new Dictionary<string, Response>
            {
                {"200", new Response {Description = "Success"}}
            };

            pathItem.Post = operation;

            return pathItem;
        }
    }

    [Route("swagger/[controller]")]
    public class ContractsController : Controller
    {
        private readonly ILoader loader;
        private readonly IStateRepositoryRoot stateRepository;
        private readonly Network network;
        private readonly SwaggerGeneratorOptions options;
        private readonly JsonSerializer swaggerSerializer;

        public ContractsController(
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
