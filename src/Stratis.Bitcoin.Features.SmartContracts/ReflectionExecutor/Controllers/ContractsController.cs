using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public string Param1 { get; set; }

        public int Param2 { get; set; }
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

            var swaggerGen = new SwaggerGenerator(apiDescriptionsProvider, this.schemaRegistryFactory, this.options);

            // Need to build a swagger doc with our dynamic schema and our generic contract invocation endpoint.
            var doc = swaggerGen.GetSwagger("contracts");
            doc.Paths.Add($"/api/contract/{address}/Test", doc.Paths["/api/contract/schema"]);
            doc.Paths.Remove("/api/contract/{address}/{method}");
            doc.Paths.Remove("/api/contract/schema");

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
