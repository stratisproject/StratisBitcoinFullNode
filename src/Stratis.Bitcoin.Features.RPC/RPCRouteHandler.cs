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

            var ms = new MemoryStream();
            await context.HttpContext.Request.Body.CopyToAsync(ms);
            context.HttpContext.Request.Body = ms;
            ms.Position = 0;
            JObject req = JObject.Load(new JsonTextReader(new StreamReader(ms)));
            ms.Position = 0;
            string method = (string)req["method"];

            string controllerName = this.actionDescriptor.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                    .FirstOrDefault(w => w.ActionName == method)?.ControllerName ?? string.Empty;

            context.RouteData.Values.Add("action", method);
            //TODO: Need to be extensible
            context.RouteData.Values.Add("controller", controllerName);
            context.RouteData.Values.Add("req", req);
            await this.inner.RouteAsync(context);
        }
    }
}
