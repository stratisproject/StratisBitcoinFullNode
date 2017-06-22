using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Moq;
using Stratis.Bitcoin.RPC;
using Xunit;
using Microsoft.AspNetCore.Http.Features;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Stratis.Bitcoin.Tests.RPC
{
	public class RPCRouteHandlerTest
	{
		private Mock<IRouter> inner;
		private Mock<IActionDescriptorCollectionProvider> actionDescriptor;
		private RPCRouteHandler handler;

		public RPCRouteHandlerTest()
		{
			this.inner = new Mock<IRouter>();
			this.actionDescriptor = new Mock<IActionDescriptorCollectionProvider>();
			this.handler = new RPCRouteHandler(this.inner.Object, this.actionDescriptor.Object);
		}

		[Fact]
		public void GetVirtualPathReturnsInnerVirtualPath()
		{
			this.inner.Setup(i => i.GetVirtualPath(It.IsAny<VirtualPathContext>()))
				.Returns(new VirtualPathData(this.inner.Object, "/~/root/"));

			var context = new VirtualPathContext(new DefaultHttpContext(), new RouteValueDictionary(), new RouteValueDictionary());
			var result = this.handler.GetVirtualPath(context);

			Assert.Equal("/~/root/", result.VirtualPath);
			Assert.Equal(this.inner.Object, result.Router);
		}

		[Fact]
		public void RouteAsyncLoadsRouteContextOntoRouteData()
		{
			var request = new HttpRequestFeature();
			SetupRequestBody(request, "{\"method\": \"GET\"}");
			var context = new RouteContext(PrepareDefaultHttpContext(request));
			RouteContext callback = null;
			this.inner.Setup(i => i.RouteAsync(It.IsAny<RouteContext>()))
				.Callback<RouteContext>((r) => { callback = r; })
				.Returns(Task.FromResult(0));
			var desciptors = new ActionDescriptorCollection(new List<ControllerActionDescriptor>()
			{
				new ControllerActionDescriptor()
				{
					ActionName = "GET",
					ControllerName = "RPCController"
				}
			}, 1);
			actionDescriptor.Setup(a => a.ActionDescriptors)
				.Returns(desciptors);			

			var task = this.handler.RouteAsync(context);
			task.Wait();

			Assert.Equal("GET", callback.RouteData.Values["action"]);
			Assert.Equal("RPCController", callback.RouteData.Values["controller"]);
			Assert.True(callback.RouteData.Values["req"].GetType() == typeof(JObject));
		}

		[Fact]
		public async Task RouteAsyncLoadsRouteContextWithoutControllerDescriptorOntoRouteData()
		{
			var request = new HttpRequestFeature();
			SetupRequestBody(request, "{\"method\": \"GET\"}");
			var context = new RouteContext(PrepareDefaultHttpContext(request));
			RouteContext callback = null;
			this.inner.Setup(i => i.RouteAsync(It.IsAny<RouteContext>()))
				.Callback<RouteContext>((r) => { callback = r; })
				.Returns(Task.FromResult(0));
			var desciptors = new ActionDescriptorCollection(new List<ControllerActionDescriptor>(), 1);
			actionDescriptor.Setup(a => a.ActionDescriptors)
				.Returns(desciptors);

			await this.handler.RouteAsync(context);

			Assert.Equal("GET", callback.RouteData.Values["action"]);
			Assert.Equal(string.Empty, callback.RouteData.Values["controller"]);
			Assert.True(callback.RouteData.Values["req"].GetType() == typeof(JObject));
		}

		private static void SetupRequestBody(HttpRequestFeature request, string requestBody)
		{
			request.Body = new MemoryStream();
			var bytes = Encoding.ASCII.GetBytes(requestBody);
			request.Body.Write(bytes, 0, bytes.Length);
			request.Body.Position = 0;
		}

		private DefaultHttpContext PrepareDefaultHttpContext(IHttpRequestFeature request)
		{
			var httpContext = new DefaultHttpContext();
			var featureCollection = new FeatureCollection();
			featureCollection.Set<IHttpRequestFeature>(request);
			httpContext.Initialize(featureCollection);

			return httpContext;
		}
	}
}

