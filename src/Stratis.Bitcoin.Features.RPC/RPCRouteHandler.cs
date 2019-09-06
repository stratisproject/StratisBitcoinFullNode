using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC
{
    public interface IRPCRouteHandler : IRouter
    {
    }

    public class RPCRouteHandler : IRPCRouteHandler
    {
        private IRouter inner;

        private IActionDescriptorCollectionProvider actionDescriptor;

        public RPCRouteHandler(IRouter inner, IActionDescriptorCollectionProvider actionDescriptor)
        {
            Guard.NotNull(inner, nameof(inner));
            Guard.NotNull(actionDescriptor, nameof(actionDescriptor));

            this.inner = inner;
            this.actionDescriptor = actionDescriptor;
        }

        public VirtualPathData GetVirtualPath(VirtualPathContext context)
        {
            Guard.NotNull(context, nameof(context));

            return this.inner.GetVirtualPath(context);
        }

        public async Task RouteAsync(RouteContext context)
        {
            Guard.NotNull(context, nameof(context));

            JToken request;
            using (StreamReader streamReader = new StreamReader(context.HttpContext.Request.Body))
            using (JsonTextReader textReader = new JsonTextReader(streamReader))
            {
                request = await JObject.LoadAsync(textReader);
            }
            
            string method = (string)request["method"];
            string controllerName = this.actionDescriptor.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                    .FirstOrDefault(w => w.ActionName == method)?.ControllerName ?? string.Empty;

            context.RouteData.Values.Add("action", method);
            //TODO: Need to be extensible
            context.RouteData.Values.Add("controller", controllerName);
            context.RouteData.Values.Add("req", request);
            await this.inner.RouteAsync(context);
        }
    }
}
