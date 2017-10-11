using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Tests.Logging;
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

            this.middleware = new RPCMiddleware(this.delegateContext.Object, this.authorization.Object, this.LoggerFactory.Object);
		}

		[Fact]
		public async Task InvokeValidAuthorizationReturns200Async()
		{
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
		}

		[Fact]
		public async Task InvokeUnauthorizedReturns401Async()
		{
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(false);

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[Fact]
		public async Task InvokeAuthorizedWithoutAuthorizationHeaderReturns401Async()
		{
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[Fact]
		public async Task InvokeAuthorizedWithBearerAuthorizationHeaderReturns401Async()
		{
			this.request.Headers.Add("Authorization", "Bearer hiuehewuytwe");
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[Fact]
		public async Task InvokeAuthorizedWithEmptyAuthorizationHeaderReturns401Async()
		{
			this.request.Headers.Add("Authorization", "");
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[Fact]
		public async Task InvokeAuthorizedWithBasicAuthorizationHeaderForUnauthorizedUserReturns401Async()
		{
			var header = Convert.ToBase64String(Encoding.ASCII.GetBytes("MyUser"));
			this.request.Headers.Add("Authorization", "Basic " + header);
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);
			this.authorization.Setup(a => a.IsAuthorized("MyUser"))
				.Returns(false);

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[Fact]
		public async Task InvokeAuthorizedWithBasicAuthorizationHeaderWithInvalidEncodingReturns401Async()
		{
			this.request.Headers.Add("Authorization", "Basic kljseuhtiuorewytiuoer");
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			Assert.Equal(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[Fact]
		public async Task InvokeThrowsArgumentExceptionWritesArgumentErrorAsync()
		{
			this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
				.Throws(new ArgumentException("Name is required."));
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -1,{0}    \"message\": \"Argument error: Name is required.\"{0}  }}{0}}}", Environment.NewLine);
				Assert.Equal(expected, reader.ReadToEnd());
				Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
			}
		}

		[Fact]
		public async Task InvokeThrowsFormatExceptionWritesArgumentErrorAsync()
		{
			this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
				.Throws(new FormatException("Int x is invalid format."));
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -1,{0}    \"message\": \"Argument error: Int x is invalid format.\"{0}  }}{0}}}", Environment.NewLine);
				Assert.Equal(expected, reader.ReadToEnd());
				Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
			}
		}

		[Fact]
		public async Task Invoke404WritesMethodNotFoundErrorAsync()
		{
			this.response.StatusCode = StatusCodes.Status404NotFound;
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -32601,{0}    \"message\": \"Method not found\"{0}  }}{0}}}", Environment.NewLine);
				Assert.Equal(expected, reader.ReadToEnd());
				Assert.Equal(StatusCodes.Status404NotFound, this.httpContext.Response.StatusCode);
			}
		}

		[Fact]
		public async Task Invoke500WritesInternalErrorAndLogsResultAsync()
		{
			this.response.StatusCode = StatusCodes.Status500InternalServerError;
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -32603,{0}    \"message\": \"Internal error\"{0}  }}{0}}}", Environment.NewLine);
				Assert.Equal(expected, reader.ReadToEnd());
				Assert.Equal(StatusCodes.Status500InternalServerError, this.httpContext.Response.StatusCode);
                base.AssertLog(this.Logger, LogLevel.Error, "Internal error while calling RPC Method");
            }
		}

		[Fact]
		public async Task InvokeThrowsUnhandledExceptionWritesInternalErrorAndLogsResultAsync()
		{
			this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
				.Throws(new InvalidOperationException("Operation not valid."));
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

            await this.middleware.Invoke(this.httpContext).ConfigureAwait(false);

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = string.Format("{{{0}  \"result\": null,{0}  \"error\": {{{0}    \"code\": -32603,{0}    \"message\": \"Internal error\"{0}  }}{0}}}", Environment.NewLine);
				Assert.Equal(expected, reader.ReadToEnd());
				Assert.Equal(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
                base.AssertLog<InvalidOperationException>(this.Logger, LogLevel.Error, "Operation not valid.", "Internal error while calling RPC Method");
            }
        }

		private void SetupValidAuthorization()
		{
			var header = Convert.ToBase64String(Encoding.ASCII.GetBytes("MyUser"));
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