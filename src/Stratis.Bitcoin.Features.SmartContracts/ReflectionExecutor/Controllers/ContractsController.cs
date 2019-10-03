using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts;
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
        [Route("TransferTo")]
        [HttpPost]
        public IActionResult TransferTo([FromBody] BuildCallContractTransactionRequest value)
        {
            // This will need to proxy to the actual SC controller
            return Ok(true);
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

    public class SwaggerGenerator2 : ISwaggerProvider
    {
        private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionsProvider;
        private readonly ISchemaRegistryFactory _schemaRegistryFactory;
        private readonly SwaggerGeneratorOptions _options;

        public SwaggerGenerator2(
            IApiDescriptionGroupCollectionProvider apiDescriptionsProvider,
            ISchemaRegistryFactory schemaRegistryFactory,
            IOptions<SwaggerGeneratorOptions> optionsAccessor)
            : this(apiDescriptionsProvider, schemaRegistryFactory, optionsAccessor.Value)
        { }

        public SwaggerGenerator2(
            IApiDescriptionGroupCollectionProvider apiDescriptionsProvider,
            ISchemaRegistryFactory schemaRegistryFactory,
            SwaggerGeneratorOptions options)
        {
            _apiDescriptionsProvider = apiDescriptionsProvider;
            _schemaRegistryFactory = schemaRegistryFactory;
            _options = options ?? new SwaggerGeneratorOptions();
        }

        public SwaggerDocument GetSwagger(
            string documentName,
            string host = null,
            string basePath = null,
            string[] schemes = null)
        {
            if (!_options.SwaggerDocs.TryGetValue(documentName, out Info info))
                throw new UnknownSwaggerDocument(documentName);

            var applicableApiDescriptions = _apiDescriptionsProvider.ApiDescriptionGroups.Items
                .SelectMany(group => group.Items)
                .Where(apiDesc => _options.DocInclusionPredicate(documentName, apiDesc))
                .Where(apiDesc => !_options.IgnoreObsoleteActions);

            var schemaRegistry = _schemaRegistryFactory.Create();

            var swaggerDoc = new SwaggerDocument
            {
                Info = info,
                Host = host,
                BasePath = basePath,
                Schemes = schemes,
                Paths = CreatePathItems(applicableApiDescriptions, schemaRegistry),
                Definitions = schemaRegistry.Definitions,
                SecurityDefinitions = _options.SecurityDefinitions.Any() ? _options.SecurityDefinitions : null,
                Security = _options.SecurityRequirements.Any() ? _options.SecurityRequirements : null
            };

            var filterContext = new DocumentFilterContext(
                _apiDescriptionsProvider.ApiDescriptionGroups,
                applicableApiDescriptions,
                schemaRegistry);

            foreach (var filter in _options.DocumentFilters)
            {
                filter.Apply(swaggerDoc, filterContext);
            }

            return swaggerDoc;
        }

        private Dictionary<string, PathItem> CreatePathItems(
            IEnumerable<ApiDescription> apiDescriptions,
            ISchemaRegistry schemaRegistry)
        {
            return apiDescriptions
                .OrderBy(_options.SortKeySelector)
                .GroupBy(apiDesc => apiDesc)
                .ToDictionary(group => "/" + group.Key, group => CreatePathItem(group, schemaRegistry));
        }

        private PathItem CreatePathItem(
            IEnumerable<ApiDescription> apiDescriptions,
            ISchemaRegistry schemaRegistry)
        {
            var pathItem = new PathItem();

            // Group further by http method
            var perMethodGrouping = apiDescriptions
                .GroupBy(apiDesc => apiDesc.HttpMethod);

            foreach (var group in perMethodGrouping)
            {
                var httpMethod = group.Key;

                if (httpMethod == null)
                    throw new NotSupportedException(string.Format(
                        "Ambiguous HTTP method for action - {0}. " +
                        "Actions require an explicit HttpMethod binding for Swagger 2.0",
                        group.First().ActionDescriptor.DisplayName));

                if (group.Count() > 1 && _options.ConflictingActionsResolver == null)
                    throw new NotSupportedException(string.Format(
                        "HTTP method \"{0}\" & path \"{1}\" overloaded by actions - {2}. " +
                        "Actions require unique method/path combination for Swagger 2.0. Use ConflictingActionsResolver as a workaround",
                        httpMethod,
                        group.First(),
                        string.Join(",", group.Select(apiDesc => apiDesc.ActionDescriptor.DisplayName))));

                var apiDescription = (group.Count() > 1) ? _options.ConflictingActionsResolver(group) : group.Single();

                switch (httpMethod.ToUpper())
                {
                    case "GET":
                        pathItem.Get = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "PUT":
                        pathItem.Put = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "POST":
                        pathItem.Post = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "DELETE":
                        pathItem.Delete = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "OPTIONS":
                        pathItem.Options = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "HEAD":
                        pathItem.Head = CreateOperation(apiDescription, schemaRegistry);
                        break;
                    case "PATCH":
                        pathItem.Patch = CreateOperation(apiDescription, schemaRegistry);
                        break;
                }
            }

            return pathItem;
        }

        private Operation CreateOperation(
            ApiDescription apiDescription,
            ISchemaRegistry schemaRegistry)
        {
            // Try to retrieve additional metadata that's not provided by ApiExplorer
            MethodInfo methodInfo;
            var customAttributes = Enumerable.Empty<object>();

            if (apiDescription.TryGetMethodInfo(out methodInfo))
            {
                customAttributes = methodInfo.GetCustomAttributes(true)
                    .Union(methodInfo.DeclaringType.GetTypeInfo().GetCustomAttributes(true));
            }

            var isDeprecated = customAttributes.Any(attr => attr.GetType() == typeof(ObsoleteAttribute));

            var operation = new Operation
            {
                OperationId = _options.OperationIdSelector(apiDescription),
                Tags = _options.TagsSelector(apiDescription),
                Consumes = CreateConsumes(apiDescription, customAttributes),
                Produces = CreateProduces(apiDescription, customAttributes),
                Parameters = CreateParameters(apiDescription, schemaRegistry),
                Responses = CreateResponses(apiDescription, schemaRegistry),
                Deprecated = isDeprecated ? true : (bool?)null
            };

            // Assign default value for Consumes if not yet assigned AND operation contains form params
            if (operation.Consumes.Count() == 0 && operation.Parameters.Any(p => p.In == "formData"))
            {
                operation.Consumes.Add("multipart/form-data");
            }

            var filterContext = new OperationFilterContext(
                apiDescription,
                schemaRegistry,
                methodInfo);

            foreach (var filter in _options.OperationFilters)
            {
                filter.Apply(operation, filterContext);
            }

            return operation;
        }

        private IList<string> CreateConsumes(ApiDescription apiDescription, IEnumerable<object> customAttributes)
        {
            var consumesAttribute = customAttributes.OfType<ConsumesAttribute>().FirstOrDefault();

            var mediaTypes = (consumesAttribute != null)
                ? consumesAttribute.ContentTypes
                : apiDescription.SupportedRequestFormats
                    .Select(apiRequestFormat => apiRequestFormat.MediaType);

            return mediaTypes.ToList();
        }

        private IList<string> CreateProduces(ApiDescription apiDescription, IEnumerable<object> customAttributes)
        {
            var producesAttribute = customAttributes.OfType<ProducesAttribute>().FirstOrDefault();

            var mediaTypes = (producesAttribute != null)
                ? producesAttribute.ContentTypes
                : apiDescription.SupportedResponseTypes
                    .SelectMany(apiResponseType => apiResponseType.ApiResponseFormats)
                    .Select(apiResponseFormat => apiResponseFormat.MediaType)
                    .Distinct();

            return mediaTypes.ToList();
        }

        private IList<IParameter> CreateParameters(
            ApiDescription apiDescription,
            ISchemaRegistry schemaRegistry)
        {
            var applicableParamDescriptions = apiDescription.ParameterDescriptions
                .Where(paramDesc =>
                {
                    return paramDesc.Source.IsFromRequest
                        && (paramDesc.ModelMetadata == null || paramDesc.ModelMetadata.IsBindingAllowed);
                });

            return applicableParamDescriptions
                .Select(paramDesc => CreateParameter(apiDescription, paramDesc, schemaRegistry))
                .ToList();
        }

        private IParameter CreateParameter(
            ApiDescription apiDescription,
            ApiParameterDescription apiParameterDescription,
            ISchemaRegistry schemaRegistry)
        {
            // Try to retrieve additional metadata that's not directly provided by ApiExplorer
            ParameterInfo parameterInfo = null;
            PropertyInfo propertyInfo = null;
            var customAttributes = Enumerable.Empty<object>();

            //if (apiParameterDescription.TryGetParameterInfo(apiDescription, out parameterInfo))
            //    customAttributes = parameterInfo.GetCustomAttributes(true);
            //else if (apiParameterDescription.TryGetPropertyInfo(out propertyInfo))
            //    customAttributes = propertyInfo.GetCustomAttributes(true);

            var name = _options.DescribeAllParametersInCamelCase
                ? apiParameterDescription.Name
                : apiParameterDescription.Name;

            var isRequired = customAttributes.Any(attr =>
                new[] { typeof(RequiredAttribute), typeof(BindRequiredAttribute) }.Contains(attr.GetType()));

            var parameter = (apiParameterDescription.Source == BindingSource.Body)
                ? CreateBodyParameter(
                    apiParameterDescription,
                    name,
                    isRequired,
                    schemaRegistry)
                : CreateNonBodyParameter(
                    apiParameterDescription,
                    parameterInfo,
                    customAttributes,
                    name,
                    isRequired,
                    schemaRegistry);

            var filterContext = new ParameterFilterContext(
                apiParameterDescription,
                schemaRegistry,
                parameterInfo,
                propertyInfo);

            foreach (var filter in _options.ParameterFilters)
            {
                filter.Apply(parameter, filterContext);
            }

            return parameter;
        }

        private IParameter CreateBodyParameter(
            ApiParameterDescription apiParameterDescription,
            string name,
            bool isRequired,
            ISchemaRegistry schemaRegistry)
        {
            var schema = schemaRegistry.GetOrRegister(apiParameterDescription.Type);

            return new BodyParameter { Name = name, Schema = schema, Required = isRequired };
        }

        private IParameter CreateNonBodyParameter(
            ApiParameterDescription apiParameterDescription,
            ParameterInfo parameterInfo,
            IEnumerable<object> customAttributes,
            string name,
            bool isRequired,
            ISchemaRegistry schemaRegistry)
        {
            var location = ParameterLocationMap.ContainsKey(apiParameterDescription.Source)
                ? ParameterLocationMap[apiParameterDescription.Source]
                : "query";

            var nonBodyParam = new NonBodyParameter
            {
                Name = name,
                In = location,
                Required = (location == "path") ? true : isRequired,
            };

            if (apiParameterDescription.Type == null)
            {
                nonBodyParam.Type = "string";
            }
            else if (typeof(IFormFile).IsAssignableFrom(apiParameterDescription.Type))
            {
                nonBodyParam.Type = "file";
            }
            else
            {
                // Retrieve a Schema object for the type and copy common fields onto the parameter
                var schema = schemaRegistry.GetOrRegister(apiParameterDescription.Type);

                // NOTE: While this approach enables re-use of SchemaRegistry logic, it introduces complexity
                // and constraints elsewhere (see below) and needs to be refactored!

                if (schema.Ref != null)
                {
                    // The registry created a referenced Schema that needs to be located. This means it's not neccessarily
                    // exclusive to this parameter and so, we can't assign any parameter specific attributes or metadata.
                    schema = schemaRegistry.Definitions[schema.Ref.Replace("#/definitions/", string.Empty)];
                }
                else
                {
                    // It's a value Schema. This means it's exclusive to this parameter and so, we can assign
                    // parameter specific attributes and metadata. Yep - it's hacky!
                    schema.Default = (parameterInfo != null && parameterInfo.IsOptional)
                        ? parameterInfo.DefaultValue
                        : null;
                }

            }

            return nonBodyParam;
        }

        private IDictionary<string, Response> CreateResponses(
            ApiDescription apiDescription,
            ISchemaRegistry schemaRegistry)
        {
            var supportedApiResponseTypes = apiDescription.SupportedResponseTypes
                .DefaultIfEmpty(new ApiResponseType { StatusCode = 200 });

            return supportedApiResponseTypes
                .ToDictionary(
                    apiResponseType => apiResponseType.StatusCode.ToString(),
                    apiResponseType => CreateResponse(apiResponseType, schemaRegistry));
        }

        private Response CreateResponse(ApiResponseType apiResponseType, ISchemaRegistry schemaRegistry)
        {
            var description = ResponseDescriptionMap
                .FirstOrDefault((entry) => Regex.IsMatch(apiResponseType.StatusCode.ToString(), entry.Key))
                .Value;

            return new Response
            {
                Description = description,
                Schema = (apiResponseType.Type != null && apiResponseType.Type != typeof(void))
                    ? schemaRegistry.GetOrRegister(apiResponseType.Type)
                    : null
            };
        }

        private static Dictionary<BindingSource, string> ParameterLocationMap = new Dictionary<BindingSource, string>
        {
            { BindingSource.Form, "formData" },
            { BindingSource.FormFile, "formData" },
            { BindingSource.Body, "body" },
            { BindingSource.Header, "header" },
            { BindingSource.Path, "path" },
            { BindingSource.Query, "query" }
        };

        private static readonly Dictionary<string, string> ResponseDescriptionMap = new Dictionary<string, string>
        {
            { "1\\d{2}", "Information" },
            { "2\\d{2}", "Success" },
            { "3\\d{2}", "Redirect" },
            { "400", "Bad Request" },
            { "401", "Unauthorized" },
            { "403", "Forbidden" },
            { "404", "Not Found" },
            { "405", "Method Not Allowed" },
            { "406", "Not Acceptable" },
            { "408", "Request Timeout" },
            { "409", "Conflict" },
            { "4\\d{2}", "Client Error" },
            { "5\\d{2}", "Server Error" }
        };
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

            var swaggerGen = new SwaggerGenerator(apiDescriptionsProvider, this.schemaRegistryFactory, this.options);

            //var sp = new ContractSwaggerGenerator(desc, this.options, address, this.stateRepository, this.network);
            var doc = swaggerGen.GetSwagger("contracts");

            var swagger = this.existingGenerator.GetSwagger("contracts", host: null, basePath: null);

            var jsonBuilder = new StringBuilder();
            using (var writer = new StringWriter(jsonBuilder))
            {
                this.swaggerSerializer.Serialize(writer, swagger);
                var j = writer.ToString();
                return Ok(j);
            }

        }

    }
}
