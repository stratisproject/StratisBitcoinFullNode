using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.Core.State;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    public class ControllerThatWillEventuallyBeDynamicallyGenerated : Controller
    {
        /// <summary>
        /// Gets the bytecode for a smart contract as a hexadecimal string. The bytecode is decompiled to
        /// C# source, which is returned as well. Be aware, it is the bytecode which is being executed,
        /// so this is the "source of truth".
        /// </summary>
        ///
        /// <param name="value">The address of the smart contract to retrieve as bytecode and C# source.</param>
        ///
        /// <returns>A response object containing the bytecode and the decompiled C# code.</returns>
        [Route("api/contract/{address}/{method}")]
        [HttpPost]
        public IActionResult TransferTo([FromRoute] string address, [FromRoute] string method)
        {
            string requestBody;
            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            // TODO map request body to JSON object, extract transaction-related params, build new request model, then call the regular SC controller.

            // Map parameters to our contract object and try to invoke it.
            // This will need to proxy to the actual SC controller

            //yield return new object[] { true }; // MethodParameterDataType.Bool
            //yield return new object[] { (byte)1 }; // MethodParameterDataType.Byte
            //yield return new object[] { Encoding.UTF8.GetBytes("test") }; // MethodParameterDataType.ByteArray
            //yield return new object[] { 's' }; // MethodParameterDataType.Char
            //yield return new object[] { "test" }; // MethodParameterDataType.String
            //yield return new object[] { (uint)36 }; // MethodParameterDataType.UInt
            //yield return new object[] { (ulong)29 }; // MethodParameterDataType.ULong
            //yield return new object[] { new uint160("0x0000000000000000000000000000000000000001").ToBase58Address(Network) }; // MethodParameterDataType.Address
            //yield return new object[] { (long)12312321 }; // MethodParameterDataType.Long,
            //yield return new object[] { (int)10000000 };// MethodParameterDataType.Int
            return Ok(address);
        }

        [Route("api/contract/schema")]
        [HttpPost]
        public IActionResult Schema(
            [FromBody] SomeSchema schema)
        {
            // Just to see how the schema is generated
            return Ok();
        }
    }

    public class SomeSchema
    {
        public bool AcceptsBool { get; set; }
        public byte AcceptsByte { get; set; }
        public byte[] AcceptsByteArray { get; set; }
        public char AcceptsChar { get; set; }
        public string AcceptsString { get; set; }
        public uint AcceptsUint { get; set; }
        public ulong AcceptsUlong { get; set; }
        public int AcceptsInt { get; set; }
        public long AcceptsLong { get; set; }
        public string AcceptsAddress { get; set; }
    }

    public class ContractSwaggerDocGenerator : ISwaggerProvider
    {
        private readonly ISchemaRegistryFactory schemaRegistryFactory;
        private readonly string address;
        private readonly ContractAssembly assembly;
        private readonly SwaggerGeneratorOptions options;

        public ContractSwaggerDocGenerator(SwaggerGeneratorOptions options, ISchemaRegistryFactory schemaRegistryFactory, string address, Assembly assembly)
        {
            this.schemaRegistryFactory = schemaRegistryFactory;
            this.address = address;
            this.assembly = new ContractAssembly(assembly);
            this.options = options;
        }

        public SwaggerDocument GetSwagger(string documentName, string host = null, string basePath = null, string[] schemes = null)
        {
            if (!this.options.SwaggerDocs.TryGetValue(documentName, out Info info))
                throw new UnknownSwaggerDocument(documentName);

            // Schemas required:
            // - Dynamic schema for the contract + some params
            // - Static schema for the response
            // Paths required:
            // - Endpoint for the dynamic API
            // - Params
            IDictionary<string, Schema> definitions = this.CreateDefinitions();

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

            // TODO: Generate GETs to perform local calls for properties.

            // The endpoint for this contract.
            // TODO: Test => MethodName
            var path = $"/api/contract/{this.address}/Test";

            var pathItem = new PathItem();

            var operation = new Operation();

            // Tag should be the contract address?
            operation.Tags = new [] { this.address };
            operation.OperationId = "Test2"; // TODO - Method name
            operation.Consumes = new[] { "application/json", "text/json", "application/*+json" };

            // TODO: Generate a bodyParam for each method.
            var bodyParam = new BodyParameter
            {
                Name = "MethodName",
                In = "body",
                Required = true,
                //Schema = schema
            };

            operation.Parameters = new List<IParameter> {bodyParam};
            operation.Responses = new Dictionary<string, Response>
            {
                {"200", new Response {Description = "Success"}}
            };

            pathItem.Post = operation;

            return new Dictionary<string, PathItem>
            {
                { path, pathItem }
            };
        }
    }

    public class ContractApiDescriptionsProvider : IApiDescriptionGroupCollectionProvider
    {
        private IApiDescriptionGroupCollectionProvider baseProvider;
        private readonly string controllerName;

        public ContractApiDescriptionsProvider(IApiDescriptionGroupCollectionProvider baseProvider, string controllerName)
        {
            this.baseProvider = baseProvider;
            this.controllerName = controllerName;
        }

        public ApiDescriptionGroupCollection ApiDescriptionGroups
        {
            get
            {
                var firstGroup = this.baseProvider.ApiDescriptionGroups.Items.First();
                var name = firstGroup.GroupName;
                var items = firstGroup.Items
                    .Where(i =>
                    {
                        if (i.ActionDescriptor is ControllerActionDescriptor descriptor)
                        {
                            return descriptor.ControllerName == this.controllerName;
                        }
                        return false;
                    })
                    .ToList();

                var group = new ApiDescriptionGroup(name, items);
                return new ApiDescriptionGroupCollection(new List<ApiDescriptionGroup> { group }, this.baseProvider.ApiDescriptionGroups.Version);
            }
        }
    }

    [Route("swagger/[controller]")]
    public class ContractsController : Controller
    {
        private readonly ISwaggerProvider existingGenerator;
        private readonly IApiDescriptionGroupCollectionProvider apiDescriptionGroupCollectionProvider;
        private readonly ISchemaRegistryFactory schemaRegistryFactory;
        private readonly IActionDescriptorChangeProvider changeProvider;
        private readonly ApplicationPartManager partManager;
        private readonly IActionDescriptorCollectionProvider actionDescriptorCollectionProvider;
        private readonly ISwaggerProvider swaggerProvider;
        private readonly IApiDescriptionGroupCollectionProvider desc;
        private readonly IStateRepositoryRoot stateRepository;
        private readonly Network network;
        private readonly SwaggerGeneratorOptions options;
        private readonly SwaggerUIOptions uiOptions;
        private JsonSerializer swaggerSerializer;

        public ContractsController(IOptions<MvcJsonOptions> mvcJsonOptions,
            ISwaggerProvider existingGenerator, IApiDescriptionGroupCollectionProvider apiDescriptionGroupCollectionProvider, ISchemaRegistryFactory schemaRegistryFactory, IActionDescriptorChangeProvider changeProvider, ApplicationPartManager partManager, IActionDescriptorCollectionProvider actionDescriptorCollectionProvider, ISwaggerProvider swaggerProvider, IApiDescriptionGroupCollectionProvider desc, IOptions<SwaggerGeneratorOptions> options, IOptions<SwaggerUIOptions> uiOptions, IStateRepositoryRoot stateRepository, Network network)
        {
            this.existingGenerator = existingGenerator;
            this.apiDescriptionGroupCollectionProvider = apiDescriptionGroupCollectionProvider;
            this.schemaRegistryFactory = schemaRegistryFactory;
            this.changeProvider = changeProvider;
            this.partManager = partManager;
            this.actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
            this.swaggerProvider = swaggerProvider;
            this.desc = desc;
            this.stateRepository = stateRepository;
            this.network = network;
            this.options = options.Value;
            this.uiOptions = uiOptions.Value;
            this.swaggerSerializer = SwaggerSerializerFactory.Create(mvcJsonOptions);
        }

        [Route("{address}")]
        [HttpGet]
        [SwaggerOperation(description: "test")]
        public async Task<IActionResult> ContractSwaggerDoc(string address)
        {
            // Attempt to dynamically generate a swagger doc based on a controller that we specify.


            //this.changeProvider.GetChangeToken();
            // Add the new assembly with the controller to the application parts.
            //this.partManager.ApplicationParts.Add(new AssemblyPart());

            // Controller should be registered. Generate a swagger doc for it.
            // DocInclusionPredicate? should be used to match the document name == "contract" and the apiDesc items?
            // Or just inject our own APIDescriptionsProvider with only the items we need
            
            var apiDescriptionsProvider = new ContractApiDescriptionsProvider(this.apiDescriptionGroupCollectionProvider, nameof(ControllerThatWillEventuallyBeDynamicallyGenerated));

            // TODO don't modify the options object directly.
            //this.options.DocInclusionPredicate = (s, description) => true;

            // We can get the controller method, then customize the available parameters
            var expected = apiDescriptionsProvider.ApiDescriptionGroups;

            var code = this.stateRepository.GetCode(address.ToUint160(this.network));

            if (code == null)
                throw new Exception("Contract does not exist");

            var assembly = Assembly.Load(code);

            // We want to skip this and implement our own one I guess
            var swaggerGen = new ContractSwaggerDocGenerator(this.options, this.schemaRegistryFactory, address, assembly);

            // Need to build a swagger doc with our dynamic schema and our generic contract invocation endpoint.
            var doc = swaggerGen.GetSwagger("contracts");
            
            //doc.Paths.Add($"/api/contract/{address}/Test", doc.Paths["/api/contract/schema"]);
            //doc.Paths.Remove("/api/contract/{address}/{method}");
            //doc.Paths.Remove("/api/contract/schema");

            // Rewrite params


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
