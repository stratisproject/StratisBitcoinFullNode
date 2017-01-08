using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Stratis.Bitcoin.RPC
{
    public static class Extensions
    {

		public static IApplicationBuilder UseRPC(this IApplicationBuilder app)
		{
			return app.UseMvc(o =>
			 {
				 o.Routes.Add(new RPCRouteHandler(o.DefaultHandler));
			 });
		}

	}
}
