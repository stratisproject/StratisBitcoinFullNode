using Microsoft.AspNetCore.Routing;
using NBitcoin.RPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using NBitcoin;

namespace Stratis.Bitcoin.RPC
{
	public class RPCRouteHandler : IRouter
	{
		private Dictionary<string, string> contollerMapping;

		IRouter _Inner;
		private IActionDescriptorCollectionProvider actionDescriptor;

		public RPCRouteHandler(IRouter inner, IActionDescriptorCollectionProvider actionDescriptor)
		{
			_Inner = inner;
			this.actionDescriptor = actionDescriptor;
		}

		public VirtualPathData GetVirtualPath(VirtualPathContext context)
		{
			return _Inner.GetVirtualPath(context);
		}

		public async Task RouteAsync(RouteContext context)
		{
			MemoryStream ms = new MemoryStream();
			await context.HttpContext.Request.Body.CopyToAsync(ms);
			context.HttpContext.Request.Body = ms;
			ms.Position = 0;
			var req = JObject.Load(new JsonTextReader(new StreamReader(ms)));
			ms.Position = 0;
			var method = (string) req["method"];

			var controllerName =
				actionDescriptor.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
					.FirstOrDefault(w => w.ActionName == method)?.ControllerName ?? string.Empty;


			context.RouteData.Values.Add("action", method);
			//TODO: Need to be extensible
			context.RouteData.Values.Add("controller", controllerName);
			context.RouteData.Values.Add("req", req);
			await _Inner.RouteAsync(context);
		}
	}
}
