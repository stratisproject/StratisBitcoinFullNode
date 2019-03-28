using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.RPC.Controllers
{
    /// <summary>
    /// Controller providing API operations on the RPC feature.
    /// </summary>
    [Route("api/[controller]")]
    public class RPCController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Full Node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>RPC Settings.</summary>
        private readonly RpcSettings rpcSettings;

        /// <summary>RPC Client Factory.</summary>
        private readonly IRPCClientFactory rpcClientFactory;

        /// <summary>ControllerActionDescriptor dictionary.</summary>
        private Dictionary<string, ControllerActionDescriptor> ActionDescriptors { get; set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="fullNode">The full node.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="rpcSettings">The RPC Settings of the full node.</param>
        public RPCController(IFullNode fullNode, ILoggerFactory loggerFactory, RpcSettings rpcSettings, IRPCClientFactory rpcClientFactory)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(rpcSettings, nameof(rpcSettings));
            Guard.NotNull(rpcClientFactory, nameof(rpcClientFactory));

            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.rpcSettings = rpcSettings;
            this.rpcClientFactory = rpcClientFactory;
        }

        /// <summary>
        /// Returns the collection of available action descriptors.
        /// </summary>
        private Dictionary<string, ControllerActionDescriptor> GetActionDescriptors()
        {
            if (this.ActionDescriptors == null)
            {
                this.ActionDescriptors = new Dictionary<string, ControllerActionDescriptor>();
                var actionDescriptorProvider = this.fullNode?.RPCHost.Services.GetService(typeof(IActionDescriptorCollectionProvider)) as IActionDescriptorCollectionProvider;
                // This approach is similar to the one used by RPCRouteHandler so should only give us the descriptors
                // that RPC would normally act on subject to the method name matching the "ActionName".
                foreach (ControllerActionDescriptor actionDescriptor in actionDescriptorProvider?.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
                    this.ActionDescriptors[actionDescriptor.ActionName] = actionDescriptor;
            }

            return this.ActionDescriptors;
        }

        /// <summary>
        /// Processes a RPCRequest.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <returns></returns>
        private RPCResponse SendRPCRequest(RPCRequest request)
        {
            // Find the binding to 127.0.0.1 or the first available. The logic in RPC settings ensures there will be at least 1.
            IPEndPoint nodeEndPoint = this.rpcSettings.Bind.Where(b => b.Address.ToString() == "127.0.0.1").FirstOrDefault() ?? this.rpcSettings.Bind[0];
            IRPCClient rpcClient = this.rpcClientFactory.Create($"{this.rpcSettings.RpcUser}:{this.rpcSettings.RpcPassword}", new Uri($"http://{nodeEndPoint}"), this.fullNode.Network);            

            return rpcClient.SendCommand(request);
        }

        /// <summary>
        /// Call an RPC method by name.
        /// </summary>
        /// <param name="body">The request to process.</param>
        /// <returns>A JSON result that varies depending on the RPC method.</returns>
        [Route("callbyname")]
        [HttpPost]
        public IActionResult CallByName([FromBody]JObject body)
        {
            Guard.NotNull(body, nameof(body));

            try
            {
                if (!this.rpcSettings.Server)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed", "Method not allowed when RPC is disabled.");
                }

                StringComparison ignoreCase = StringComparison.InvariantCultureIgnoreCase;
                string methodName = (string)body.GetValue("methodName", ignoreCase);

                ControllerActionDescriptor actionDescriptor = null;
                if (!this.GetActionDescriptors()?.TryGetValue(methodName, out actionDescriptor) ?? false)
                    throw new Exception($"RPC method '{ methodName }' not found.");

                // Prepare the named parameters that were passed via the query string in the order that they are expected by SendCommand.
                List<ControllerParameterDescriptor> paramInfos = actionDescriptor.Parameters.OfType<ControllerParameterDescriptor>().ToList();

                var paramsAsObjects = new object[paramInfos.Count];
                for (int i = 0; i < paramInfos.Count; i++)
                {
                    ControllerParameterDescriptor pInfo = paramInfos[i];
                    bool hasValue = body.TryGetValue(pInfo.Name, ignoreCase, out JToken jValue);
                    if (hasValue && !jValue.HasValues)
                    {
                        paramsAsObjects[i] = jValue.ToString();
                    }
                    else
                    {
                        paramsAsObjects[i] = pInfo.ParameterInfo.DefaultValue?.ToString();
                    }
                }

                RPCRequest request = new RPCRequest(methodName, paramsAsObjects);

                // Build RPC request object.
                RPCResponse response = this.SendRPCRequest(request);

                // Throw error if any.
                if (response?.Error?.Message != null)
                    throw new Exception(response.Error.Message);

                // Return a Json result from the API call.
                return this.Json(response?.Result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Lists the available RPC methods.
        /// </summary>
        /// <returns>A JSON result that lists the RPC methods.</returns>
        [Route("listmethods")]
        [HttpGet]
        public IActionResult ListMethods()
        {
            try
            {
                if (!this.rpcSettings.Server)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.MethodNotAllowed, "Method not allowed", "Method not allowed when RPC is disabled.");
                }

                var listMethods = new List<Models.RpcCommandModel>();
                foreach (ControllerActionDescriptor descriptor in this.GetActionDescriptors().Values.Where(desc => desc.ActionName == desc.ActionName.ToLower()))
                {
                    CustomAttributeData attr = descriptor.MethodInfo.CustomAttributes.Where(x => x.AttributeType == typeof(ActionDescription)).FirstOrDefault();
                    string description = attr?.ConstructorArguments.FirstOrDefault().Value as string ?? "";

                    var parameters = new List<string>();
                    foreach (ControllerParameterDescriptor param in descriptor.Parameters.OfType<ControllerParameterDescriptor>())
                    {
                        if (!param.ParameterInfo.IsRetval)
                        {
                            string value = $"<{param.ParameterInfo.Name.ToLower()}>";

                            if (param.ParameterInfo.HasDefaultValue)
                                value = $"[{value}]";

                            parameters.Add(value);
                        }
                    }

                    string method = $"{descriptor.ActionName} {string.Join(" ", parameters.ToArray())}";

                    listMethods.Add(new Models.RpcCommandModel { Command = method.Trim(), Description = description });
                }

                return this.Json(listMethods);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
