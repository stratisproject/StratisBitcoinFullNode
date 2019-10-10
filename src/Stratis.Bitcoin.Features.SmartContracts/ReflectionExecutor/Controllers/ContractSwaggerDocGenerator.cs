using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.SmartContracts.CLR.Loader;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    /// <summary>
    /// Creates swagger documents for a contract assembly.
    /// Maps the methods of a contract and its parameters to a call endpoint.
    /// Maps the properties of a contract to an local call endpoint.
    /// </summary>
    public class ContractSwaggerDocGenerator : ISwaggerProvider
    {
        private readonly string address;
        private readonly IContractAssembly assembly;
        private readonly string defaultWalletName;
        private readonly string defaultSenderAddress;
        private readonly SwaggerGeneratorOptions options;

        public ContractSwaggerDocGenerator(SwaggerGeneratorOptions options, string address, IContractAssembly assembly, string defaultWalletName = "", string defaultSenderAddress = "")
        {
            this.address = address;
            this.assembly = assembly;
            this.defaultWalletName = defaultWalletName;
            this.defaultSenderAddress = defaultSenderAddress;
            this.options = options;
        }

        /// <summary>
        /// Generates a swagger document for an assembly. Adds a path per public method, with a request body
        /// that contains the parameters of the method. Transaction-related metadata is added to header fields
        /// which are pre-filled with sensible defaults.
        /// </summary>
        /// <param name="documentName">The name of the swagger document to use.</param>
        /// <param name="host"></param>
        /// <param name="basePath"></param>
        /// <param name="schemes"></param>
        /// <returns></returns>
        /// <exception cref="UnknownSwaggerDocument"></exception>
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
            operation.OperationId = propertyInfo.Name;
            operation.Consumes = new[] { "application/json", "text/json", "application/*+json" };
            operation.Parameters = this.GetLocalCallMetadataHeaderParams();

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

            var parameters = new List<IParameter>
            {
                bodyParam
            };

            // Get the extra metadata fields required for a contract transaction and add this as header data.
            // We use headers few reasons:
            // - Compatibility with Swagger, which doesn't support setting multiple body objects.
            // - Preventing collisions with contract method parameter names if we were add them to method invocation body object.
            // - Still somewhat REST-ful vs. adding query params.
            // We add header params after adding the body param so they appear in the correct order.
            parameters.AddRange(this.GetCallMetadataHeaderParams());

            operation.Parameters = parameters;

            operation.Responses = new Dictionary<string, Response>
            {
                {"200", new Response {Description = "Success"}}
            };

            pathItem.Post = operation;

            return pathItem;
        }

        private List<IParameter> GetLocalCallMetadataHeaderParams()
        {
            return new List<IParameter>
            {
                new NonBodyParameter
                {
                    Name = "GasPrice",
                    In = "header",
                    Required = true,
                    Type = "number",
                    Format = "int64",
                    Minimum = SmartContractMempoolValidator.MinGasPrice,
                    Maximum = SmartContractFormatLogic.GasPriceMaximum,
                    Default = SmartContractMempoolValidator.MinGasPrice
                },
                new NonBodyParameter
                {
                    Name = "GasLimit",
                    In = "header",
                    Required = true,
                    Type = "number",
                    Format = "int64",
                    Minimum = SmartContractFormatLogic.GasLimitCallMinimum,
                    Maximum = SmartContractFormatLogic.GasLimitMaximum,
                    Default = SmartContractFormatLogic.GasLimitMaximum
                },
                new NonBodyParameter
                {
                    Name = "Amount",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = "0"
                },
                new NonBodyParameter
                {
                    Name = "Sender",
                    In = "header",
                    Required = false,
                    Type = "string",
                    Default = this.defaultSenderAddress
                }
            };
        }

        private List<IParameter> GetCallMetadataHeaderParams()
        {
            return new List<IParameter>
            {
                new NonBodyParameter
                {
                    Name = "GasPrice",
                    In = "header",
                    Required = true,
                    Type = "number",
                    Format = "int64",
                    Minimum = SmartContractMempoolValidator.MinGasPrice,
                    Maximum = SmartContractFormatLogic.GasPriceMaximum,
                    Default = SmartContractMempoolValidator.MinGasPrice
                },
                new NonBodyParameter
                {
                    Name = "GasLimit",
                    In = "header",
                    Required = true,
                    Type = "number",
                    Format = "int64",
                    Minimum = SmartContractFormatLogic.GasLimitCallMinimum,
                    Maximum = SmartContractFormatLogic.GasLimitMaximum,
                    Default = SmartContractFormatLogic.GasLimitMaximum
                },
                new NonBodyParameter
                {
                    Name = "Amount",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = "0"
                },
                new NonBodyParameter
                {
                    Name = "FeeAmount",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = "0.01"
                },
                new NonBodyParameter
                {
                    Name = "WalletName",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = this.defaultWalletName
                },
                new NonBodyParameter
                {
                    Name = "WalletPassword",
                    In = "header",
                    Required = true,
                    Type = "string"
                },
                new NonBodyParameter
                {
                    Name = "Sender",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = this.defaultSenderAddress
                }
            };
        }
    }
}