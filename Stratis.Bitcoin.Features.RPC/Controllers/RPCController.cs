using System;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NBitcoin.RPC;
using Stratis.Bitcoin.Builder.Feature;
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

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        public RPCController(IFullNode fullNode, ILoggerFactory loggerFactory)
        {
            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Finds an RPC method by name by looking in all features assemblies of the full node.
        /// </summary>
        /// <param name="name">The name of the RPC method to find.</param>
        /// <returns>The 'MethodInfo' associated with the RPC method or null if the method is not found.</returns>
        private MethodInfo GetRPCMethod(string name)
        {
            foreach (Assembly asm in this.fullNode.Services.Features.OfType<FullNodeFeature>().Select(x => x.GetType().GetTypeInfo().Assembly).Distinct())
            {
                MethodInfo methodInfo = asm.GetTypes()
                    .Where(type => typeof(Controller).IsAssignableFrom(type))
                    .SelectMany(type => type.GetMethods())
                    .Where(method => method.IsPublic && !method.IsDefined(typeof(NonActionAttribute)) && method.GetCustomAttribute<ActionNameAttribute>()?.Name == name).FirstOrDefault();

                if (methodInfo != null)
                    return methodInfo;
            }

            return null;
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
                MethodInfo methodInfo = GetRPCMethod(methodName);

                if (methodInfo == null)
                    throw new Exception($"RPC method '{ methodName }' not found.");

                var controller = this.fullNode.Services.ServiceProvider.GetService(methodInfo.DeclaringType);
                if (controller == null)
                    throw new Exception($"RPC method '{ methodName }' controller instance '{ methodInfo.DeclaringType }' not found.");

                RpcSettings rpcSettings = this.fullNode.NodeService<RpcSettings>();

                // Find the binding to 127.0.0.1 or the first available. The logic in RPC settings ensures there will be at least 1.
                System.Net.IPEndPoint nodeEndPoint = rpcSettings.Bind.Where(b => b.Address.ToString() == "127.0.0.1").FirstOrDefault() ?? rpcSettings.Bind[0];

                var rpcClient = new RPCClient($"{rpcSettings.RpcUser}:{rpcSettings.RpcPassword}", new Uri($"http://{nodeEndPoint}"), this.fullNode.Network);

                ParameterInfo[] paramInfo = methodInfo.GetParameters();
                string[] param = new string[paramInfo.Length];
                for (int i = 0; i < paramInfo.Length; i++)
                {
                    var pInfo = paramInfo[i];
                    if (this.Request.Query.TryGetValue(pInfo.Name.ToLower(), out Microsoft.Extensions.Primitives.StringValues values))
                        param[i] = values[0];
                    else
                        param[i] = pInfo.DefaultValue?.ToString();
                }

                RPCResponse response = rpcClient.SendCommand(methodName, param);

                if (response?.Error?.Message != null)
                    throw new Exception(response.Error.Message);

                return this.Json(response?.Result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
