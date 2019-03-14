using System;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests
{
    public class RPCMiddlewareTest : LogsTestBase
    {
        private Mock<IRPCAuthorization> authorization;
        private Mock<RequestDelegate> delegateContext;
        private DefaultHttpContext httpContext;
        private RPCMiddleware middleware;
        private HttpResponseFeature response;
        private FeatureCollection featureCollection;
        private HttpRequestFeature request;

        public RPCMiddlewareTest()
        {
            this.httpContext = new DefaultHttpContext();
            this.authorization = new Mock<IRPCAuthorization>();
            this.delegateContext = new Mock<RequestDelegate>();

            this.httpContext = new DefaultHttpContext();
            this.response = new HttpResponseFeature();
            this.request = new HttpRequestFeature();
            this.response.Body = new MemoryStream();
            this.featureCollection = new FeatureCollection();

            this.middleware = new RPCMiddleware(this.delegateContext.Object, this.authorization.Object, this.LoggerFactory.Object, new Mock<IHttpContextFactory>().Object, new DataFolder(string.Empty));
        }

        [Fact]
        public void InvokeValidAuthorizationReturns200()
        {
            this.SetupValidAuthorization();
            this.InitializeFeatureContext();

            this.middleware.InvokeAsync(this.httpContext).Wait();

            Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
        }

        [Fact]
        public void InvokeUnauthorizedReturns401()
        {
            this.InitializeFeatureContext();
            this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
                .Returns(false);

            this.middleware.InvokeAsync(this.httpContext).Wait();

            Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
        }

        [Fact]
        public void InvokeAuthorizedWithoutAuthorizationHeaderReturns401()
        {
            this.InitializeFeatureContext();
            this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
                .Returns(true);

            this.middleware.InvokeAsync(this.httpContext).Wait();

            Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
        }

        [Fact]
        public void InvokeAuthorizedWithBearerAuthorizationHeaderReturns401()
        {
            this.request.Headers.Add("Authorization", "Bearer hiuehewuytwe");
            this.InitializeFeatureContext();
            this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
                .Returns(true);

            this.middleware.InvokeAsync(this.httpContext).Wait();

            Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
        }

        [Fact]
        public void InvokeAuthorizedWithEmptyAuthorizationHeaderReturns401()
        {
            this.request.Headers.Add("Authorization", "");
            this.InitializeFeatureContext();
            this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
                .Returns(true);

            this.middleware.InvokeAsync(this.httpContext).Wait();

            Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
        }

        [Fact]
        public void InvokeAuthorizedWithBasicAuthorizationHeaderForUnauthorizedUserReturns401()
        {
            string header = Convert.ToBase64String(Encoding.ASCII.GetBytes("MyUser"));
            this.request.Headers.Add("Authorization", "Basic " + header);
            this.InitializeFeatureContext();
            this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
                .Returns(true);
            this.authorization.Setup(a => a.IsAuthorized("MyUser"))
                .Returns(false);

            this.middleware.InvokeAsync(this.httpContext).Wait();

            Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
        }

        [Fact]
        public void InvokeAuthorizedWithBasicAuthorizationHeaderWithInvalidEncodingReturns401()
        {
            this.request.Headers.Add("Authorization", "Basic kljseuhtiuorewytiuoer");
            this.InitializeFeatureContext();
            this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
                .Returns(true);

            this.middleware.InvokeAsync(this.httpContext).Wait();

            Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
        }

        [Fact]
        public void InvokeThrowsArgumentExceptionWritesArgumentError()
        {
            this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
                .Throws(new ArgumentException("Name is required."));
            this.SetupValidAuthorization();
            this.InitializeFeatureContext();

            this.middleware.InvokeAsync(this.httpContext).Wait();

            this.httpContext.Response.Body.Position = 0;
            using (var reader = new StreamReader(this.httpContext.Response.Body))
            {
                string expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -1,{0}    \"message\": \"Argument error: Name is required.\"{0}  }}{0}}}", Environment.NewLine);
                Assert.Equal(expected, reader.ReadToEnd());
                Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
            }
        }

        [Fact]
        public void InvokeThrowsFormatExceptionWritesArgumentError()
        {
            this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
                .Throws(new FormatException("Int x is invalid format."));
            this.SetupValidAuthorization();
            this.InitializeFeatureContext();

            this.middleware.InvokeAsync(this.httpContext).Wait();

            this.httpContext.Response.Body.Position = 0;
            using (var reader = new StreamReader(this.httpContext.Response.Body))
            {
                string expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -1,{0}    \"message\": \"Argument error: Int x is invalid format.\"{0}  }}{0}}}", Environment.NewLine);
                Assert.Equal(expected, reader.ReadToEnd());
                Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
            }
        }

        [Fact]
        public void Invoke404WritesMethodNotFoundError()
        {
            this.response.StatusCode = StatusCodes.Status404NotFound;
            this.SetupValidAuthorization();
            this.InitializeFeatureContext();

            this.middleware.InvokeAsync(this.httpContext).Wait();

            this.httpContext.Response.Body.Position = 0;
            using (var reader = new StreamReader(this.httpContext.Response.Body))
            {
                string expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -32601,{0}    \"message\": \"Method not found\"{0}  }}{0}}}", Environment.NewLine);
                Assert.Equal(expected, reader.ReadToEnd());
                Assert.Equal(StatusCodes.Status404NotFound, this.httpContext.Response.StatusCode);
            }
        }

        [Fact]
        public void Invoke500WritesInternalErrorAndLogsResult()
        {
            this.response.StatusCode = StatusCodes.Status500InternalServerError;
            this.SetupValidAuthorization();
            this.InitializeFeatureContext();

            this.middleware.InvokeAsync(this.httpContext).Wait();

            this.httpContext.Response.Body.Position = 0;
            using (var reader = new StreamReader(this.httpContext.Response.Body))
            {
                string expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -32603,{0}    \"message\": \"Internal error\"{0}  }}{0}}}", Environment.NewLine);
                Assert.Equal(expected, reader.ReadToEnd());
                Assert.Equal(StatusCodes.Status500InternalServerError, this.httpContext.Response.StatusCode);
                this.AssertLog(this.Logger, LogLevel.Error, "Internal error while calling RPC Method");
            }
        }

        [Fact]
        public void InvokeThrowsUnhandledExceptionWritesInternalErrorAndLogsResult()
        {
            this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
                .Throws(new InvalidOperationException("Operation not valid."));
            this.SetupValidAuthorization();
            this.InitializeFeatureContext();

            this.middleware.InvokeAsync(this.httpContext).Wait();

            this.httpContext.Response.Body.Position = 0;
            using (var reader = new StreamReader(this.httpContext.Response.Body))
            {
                string expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -32603,{0}    \"message\": \"Internal error\"{0}  }}{0}}}", Environment.NewLine);
                Assert.Equal(expected, reader.ReadToEnd());
                Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
                this.AssertLog<InvalidOperationException>(this.Logger, LogLevel.Error, "Operation not valid.", "Internal error while calling RPC Method");
            }
        }

        private void SetupValidAuthorization()
        {
            string header = Convert.ToBase64String(Encoding.ASCII.GetBytes("MyUser"));
            this.request.Headers.Add("Authorization", "Basic " + header);
            this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
                .Returns(true);
            this.authorization.Setup(a => a.IsAuthorized("MyUser"))
                .Returns(true);
        }

        private void InitializeFeatureContext()
        {
            this.featureCollection.Set<IHttpRequestFeature>(this.request);
            this.featureCollection.Set<IHttpResponseFeature>(this.response);
            this.httpContext.Initialize(this.featureCollection);
        }
    }
}
