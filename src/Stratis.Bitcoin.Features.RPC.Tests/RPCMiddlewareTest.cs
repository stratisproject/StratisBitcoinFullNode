using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests
{
    public class RPCMiddlewareTest : LogsTestBase
    {
        private Mock<IRPCAuthorization> authorization;
        private Mock<RequestDelegate> delegateContext;
        private Mock<IHttpContextFactory> httpContextFactory;
        private DefaultHttpContext httpContext;
        private RPCMiddleware middleware;
        private HttpResponseFeature response;
        private FeatureCollection featureCollection;
        private HttpRequestFeature request;

        public RPCMiddlewareTest()
        {
            this.authorization = new Mock<IRPCAuthorization>();
            this.delegateContext = new Mock<RequestDelegate>();

            this.httpContext = new DefaultHttpContext();
            this.response = new HttpResponseFeature();
            this.request = new HttpRequestFeature();
            this.response.Body = new MemoryStream();
            this.featureCollection = new FeatureCollection();

            this.httpContextFactory = new Mock<IHttpContextFactory>();
            this.httpContextFactory.Setup(f => f.Create(It.IsAny<FeatureCollection>())).Returns((FeatureCollection f) => {
                DefaultHttpContext newHttpContext = new DefaultHttpContext();
                newHttpContext.Initialize(f);

                return newHttpContext;
            });

            this.middleware = new RPCMiddleware(this.delegateContext.Object, this.authorization.Object, this.LoggerFactory.Object, this.httpContextFactory.Object, new DataFolder(string.Empty));
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
                JToken expected = JToken.Parse(@"{""result"": null, ""error"": {""code"": -1, ""message"": ""Argument error: Name is required.""}}");
                JToken actual = JToken.Parse(reader.ReadToEnd());
                actual.Should().BeEquivalentTo(expected);
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

            using (this.httpContext.Request.Body = new MemoryStream())
            {
                byte[] payloadBuffor = Encoding.UTF8.GetBytes(@"{""method"": ""name doesn't matter""}");
                this.httpContext.Request.Body.Write(payloadBuffor);
                this.httpContext.Request.Body.Flush();
                this.httpContext.Request.Body.Position = 0;

                this.httpContext.Request.ContentLength = payloadBuffor.Length;

                this.middleware.InvokeAsync(this.httpContext).Wait();

                this.httpContext.Response.Body.Position = 0;
                using (var reader = new StreamReader(this.httpContext.Response.Body))
                {
                    JToken expected = JToken.Parse(@"{""result"": null, ""error"": {""code"": -1, ""message"": ""Argument error: Int x is invalid format.""}}");
                    JToken actual = JToken.Parse(reader.ReadToEnd());
                    actual.Should().BeEquivalentTo(expected);
                    Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
                }
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
                JToken expected = JToken.Parse(@"{""result"": null, ""error"": {""code"": -32601, ""message"": ""Method not found""}}");
                JToken actual = JToken.Parse(reader.ReadToEnd());
                actual.Should().BeEquivalentTo(expected);
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
                JToken expected = JToken.Parse(@"{""result"": null, ""error"": { ""code"": -32603, ""message"": ""Internal error"" }}");
                JToken actual = JToken.Parse(reader.ReadToEnd());
                actual.Should().BeEquivalentTo(expected);

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

            using (this.httpContext.Request.Body = new MemoryStream())
            {
                byte[] payloadBuffor = Encoding.UTF8.GetBytes(@"{""method"": ""name doesn't matter""}");
                this.httpContext.Request.Body.Write(payloadBuffor);
                this.httpContext.Request.Body.Flush();
                this.httpContext.Request.Body.Position = 0;

                this.httpContext.Request.ContentLength = payloadBuffor.Length;

                this.middleware.InvokeAsync(this.httpContext).Wait();

                this.httpContext.Response.Body.Position = 0;
                using (var reader = new StreamReader(this.httpContext.Response.Body))
                {
                    JToken expected = JToken.Parse(@"{""result"": null, ""error"": {""code"": -32603,""message"": ""Internal error""}}");
                    JToken actual = JToken.Parse(reader.ReadToEnd());
                    actual.Should().BeEquivalentTo(expected);

                    Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
                    this.AssertLog<InvalidOperationException>(this.Logger, LogLevel.Error, "Operation not valid.", "Internal error while calling RPC Method");
                }
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
