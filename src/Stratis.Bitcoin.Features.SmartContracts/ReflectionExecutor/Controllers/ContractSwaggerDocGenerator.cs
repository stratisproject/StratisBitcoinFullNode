using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.CLR.Loader;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
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
            operation.OperationId = propertyInfo.Name;
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
}