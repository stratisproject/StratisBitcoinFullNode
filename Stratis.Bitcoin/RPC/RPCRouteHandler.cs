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

namespace Stratis.Bitcoin.RPC
{
	public class RPCRouteHandler : IRouter
	{
		IRouter _Inner;
		public RPCRouteHandler(IRouter inner)
		{
			_Inner = inner;
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
			context.RouteData.Values.Add("action", (string)req["method"]);
			//TODO: Need to be extensible
			context.RouteData.Values.Add("controller", "Consensus");
			context.RouteData.Values.Add("req", req);
			await _Inner.RouteAsync(context);
		}
	}
}
