using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Stratis.Bitcoin.Features.RPC
{
    public static class Extensions
    {
        public static IApplicationBuilder UseRPC(this IApplicationBuilder app)
        {
            return app.UseMvc(o =>
            {
                var actionDescriptor = app.ApplicationServices.GetService(typeof(IActionDescriptorCollectionProvider)) as IActionDescriptorCollectionProvider;
                o.Routes.Add(new RPCRouteHandler(o.DefaultHandler, actionDescriptor));
            });
        }
    }
}
