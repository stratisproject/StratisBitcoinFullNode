using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using NBitcoin.RPC;
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

        /// <summary>ControllerActionDescriptor dictionary.</summary>
        private Dictionary<string, ControllerActionDescriptor> ActionDescriptors {get; set;}

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="fullNode">The full node.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="rpcSettings">The RPC Settings of the full node.</param>
        public RPCController(IFullNode fullNode, ILoggerFactory loggerFactory, RpcSettings rpcSettings)
        {
            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.rpcSettings = rpcSettings;
        }

        /// <summary>
        /// Returns the collection of available action descriptors.
        /// </summary>
        private Dictionary<string, ControllerActionDescriptor> GetActionDescriptors()
        {
            if (this.ActionDescriptors == null)
            {
                this.ActionDescriptors = new Dictionary<string, ControllerActionDescriptor>();
                var actionDescriptorProvider = (this.fullNode as FullNode)?.RPCHost.Services.GetService(typeof(IActionDescriptorCollectionProvider)) as IActionDescriptorCollectionProvider;
                // This approach is similar to the one used by RPCRouteHandler so should only give us the descriptors 
                // that RPC would normally act on subject to the method name matching the "ActionName".
                foreach (var actionDescriptor in actionDescriptorProvider?.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
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
            System.Net.IPEndPoint nodeEndPoint = this.rpcSettings.Bind.Where(b => b.Address.ToString() == "127.0.0.1").FirstOrDefault() ?? this.rpcSettings.Bind[0];
            var rpcClient = new RPCClient($"{this.rpcSettings.RpcUser}:{this.rpcSettings.RpcPassword}", new Uri($"http://{nodeEndPoint}"), this.fullNode.Network);

            return rpcClient.SendCommand(request);
        }

        /// <summary>
        /// Call an RPC method by name.
        /// </summary>
        /// <returns>A JSON result that varies depending on the RPC method.</returns>
        [Route("callbyname")]
        [HttpGet]
        public IActionResult CallByName([FromQuery]string methodName)
        {
            try
            {
                ControllerActionDescriptor actionDescriptor = null;
                if (!this.GetActionDescriptors()?.TryGetValue(methodName, out actionDescriptor) ?? false)
                    throw new Exception($"RPC method '{ methodName }' not found.");

                // Prepare the named parameters that were passed via the query string in the order that they are expected by SendCommand.
                var paramInfo = actionDescriptor.Parameters.OfType<ControllerParameterDescriptor>().ToList();
                object[] param = new object[paramInfo.Count];
                for (int i = 0; i <  paramInfo.Count; i++)
                {                    
                    var pInfo = paramInfo[i];
                    var stringValues = this.Request.Query.FirstOrDefault(p => p.Key.ToLower() == pInfo.Name.ToLower());
                    param[i] = (stringValues.Key == null)?pInfo.ParameterInfo.HasDefaultValue?pInfo.ParameterInfo.DefaultValue.ToString():null:stringValues.Value[0];
                }

                // Build RPC request object.
                RPCResponse response = this.SendRPCRequest(new RPCRequest(methodName, param));

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
        public IActionResult ListMethods()
        {
            try
            {
                var listMethods = new List<Models.RpcCommandModel>();
                foreach (var descriptor in this.GetActionDescriptors().Values.Where(desc => desc.ActionName == desc.ActionName.ToLower()))
                {
                    var attr = descriptor.MethodInfo.CustomAttributes.Where(x => x.AttributeType == typeof(ActionDescription)).FirstOrDefault();
                    var description = attr?.ConstructorArguments.FirstOrDefault().Value as string ?? "";

                    var parameters = new List<string>();
                    foreach (var param in descriptor.Parameters.OfType<ControllerParameterDescriptor>())
                        if (!param.ParameterInfo.IsRetval)
                        {
                            string value = $"<{param.ParameterInfo.Name.ToLower()}>";

                            if (param.ParameterInfo.HasDefaultValue)
                                value = $"[{value}]";

                            parameters.Add(value);
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
