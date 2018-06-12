using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests
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
            VirtualPathData result = this.handler.GetVirtualPath(context);

            Assert.Equal("/~/root/", result.VirtualPath);
            Assert.Equal(this.inner.Object, result.Router);
        }

        [Fact]
        public void RouteAsyncLoadsRouteContextOntoRouteData()
        {
            var request = new HttpRequestFeature();
            SetupRequestBody(request, "{\"method\": \"GET\"}");
            var context = new RouteContext(this.PrepareDefaultHttpContext(request));
            RouteContext callback = null;
            this.inner.Setup(i => i.RouteAsync(It.IsAny<RouteContext>()))
                .Callback<RouteContext>((r) => { callback = r; })
                .Returns(Task.FromResult(0));
            var desciptors = new ActionDescriptorCollection(new List<ControllerActionDescriptor>
            {
                new ControllerActionDescriptor
                {
                    ActionName = "GET",
                    ControllerName = "RPCController"
                }
            }, 1);
            this.actionDescriptor.Setup(a => a.ActionDescriptors)
                .Returns(desciptors);

            Task task = this.handler.RouteAsync(context);
            task.Wait();

            Assert.Equal("GET", callback.RouteData.Values["action"]);
            Assert.Equal("RPCController", callback.RouteData.Values["controller"]);
            Assert.True(callback.RouteData.Values["req"].GetType() == typeof(JObject));
        }

        [Fact]
        public async Task RouteAsyncLoadsRouteContextWithoutControllerDescriptorOntoRouteDataAsync()
        {
            var request = new HttpRequestFeature();
            SetupRequestBody(request, "{\"method\": \"GET\"}");
            var context = new RouteContext(this.PrepareDefaultHttpContext(request));
            RouteContext callback = null;
            this.inner.Setup(i => i.RouteAsync(It.IsAny<RouteContext>()))
                .Callback<RouteContext>((r) => { callback = r; })
                .Returns(Task.FromResult(0));
            var desciptors = new ActionDescriptorCollection(new List<ControllerActionDescriptor>(), 1);
            this.actionDescriptor.Setup(a => a.ActionDescriptors)
                .Returns(desciptors);

            await this.handler.RouteAsync(context);

            Assert.Equal("GET", callback.RouteData.Values["action"]);
            Assert.Equal(string.Empty, callback.RouteData.Values["controller"]);
            Assert.True(callback.RouteData.Values["req"].GetType() == typeof(JObject));
        }

        private static void SetupRequestBody(HttpRequestFeature request, string requestBody)
        {
            request.Body = new MemoryStream();
            byte[] bytes = Encoding.ASCII.GetBytes(requestBody);
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
