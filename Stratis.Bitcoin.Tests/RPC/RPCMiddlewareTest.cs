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
using Stratis.Bitcoin.RPC;
using Stratis.Bitcoin.Tests.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.RPC
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

		protected override void Initialize()
		{
			this.httpContext = new DefaultHttpContext();
			this.authorization = new Mock<IRPCAuthorization>();
			this.delegateContext = new Mock<RequestDelegate>();

			this.httpContext = new DefaultHttpContext();
			this.response = new HttpResponseFeature();
			this.request = new HttpRequestFeature();
			this.response.Body = new MemoryStream();
			this.featureCollection = new FeatureCollection();					

			this.middleware = new RPCMiddleware(this.delegateContext.Object, this.authorization.Object);
		}

		[TestMethod]
		public void InvokeValidAuthorizationReturns200()
		{
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

			this.middleware.Invoke(this.httpContext).Wait();

			Assert.AreEqual(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
		}

		[TestMethod]
		public void InvokeUnauthorizedReturns401()
		{
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(false);

			this.middleware.Invoke(this.httpContext).Wait();

			Assert.AreEqual(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[TestMethod]
		public void InvokeAuthorizedWithoutAuthorizationHeaderReturns401()
		{
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);

			this.middleware.Invoke(this.httpContext).Wait();

			Assert.AreEqual(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[TestMethod]
		public void InvokeAuthorizedWithBearerAuthorizationHeaderReturns401()
		{
			this.request.Headers.Add("Authorization", "Bearer hiuehewuytwe");
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);

			this.middleware.Invoke(this.httpContext).Wait();

			Assert.AreEqual(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[TestMethod]
		public void InvokeAuthorizedWithEmptyAuthorizationHeaderReturns401()
		{
			this.request.Headers.Add("Authorization", "");
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);

			this.middleware.Invoke(this.httpContext).Wait();

			Assert.AreEqual(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[TestMethod]
		public void InvokeAuthorizedWithBasicAuthorizationHeaderForUnauthorizedUserReturns401()
		{
			var header = Convert.ToBase64String(Encoding.ASCII.GetBytes("MyUser"));
			this.request.Headers.Add("Authorization", "Basic " + header);
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);
			this.authorization.Setup(a => a.IsAuthorized("MyUser"))
				.Returns(false);

			this.middleware.Invoke(this.httpContext).Wait();

			Assert.AreEqual(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}

		[TestMethod]
		public void InvokeAuthorizedWithBasicAuthorizationHeaderWithInvalidEncodingReturns401()
		{
			this.request.Headers.Add("Authorization", "Basic kljseuhtiuorewytiuoer");
			this.InitializeFeatureContext();
			this.authorization.Setup(a => a.IsAuthorized(It.IsAny<IPAddress>()))
				.Returns(true);

			this.middleware.Invoke(this.httpContext).Wait();

			Assert.AreEqual(StatusCodes.Status401Unauthorized, this.httpContext.Response.StatusCode);
		}		

		[TestMethod]
		public void InvokeThrowsArgumentExceptionWritesArgumentError()
		{			
			this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
				.Throws(new ArgumentException("Name is required."));
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

			this.middleware.Invoke(this.httpContext).Wait();

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = "{\r\n  \"result\": null,\r\n  \"error\": {\r\n    \"code\": -1,\r\n    \"message\": \"Argument error: Name is required.\"\r\n  }\r\n}";
				Assert.AreEqual(expected, reader.ReadToEnd());
				Assert.AreEqual(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
			}
		}

		[TestMethod]
		public void InvokeThrowsFormatExceptionWritesArgumentError()
		{
			this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
				.Throws(new FormatException("Int x is invalid format."));
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

			this.middleware.Invoke(this.httpContext).Wait();

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = "{\r\n  \"result\": null,\r\n  \"error\": {\r\n    \"code\": -1,\r\n    \"message\": \"Argument error: Int x is invalid format.\"\r\n  }\r\n}";
				Assert.AreEqual(expected, reader.ReadToEnd());
				Assert.AreEqual(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
			}
		}

		[TestMethod]
		public void Invoke404WritesMethodNotFoundError()
		{
			this.response.StatusCode = StatusCodes.Status404NotFound;
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

			this.middleware.Invoke(this.httpContext).Wait();

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = "{\r\n  \"result\": null,\r\n  \"error\": {\r\n    \"code\": -32601,\r\n    \"message\": \"Method not found\"\r\n  }\r\n}";
				Assert.AreEqual(expected, reader.ReadToEnd());
				Assert.AreEqual(StatusCodes.Status404NotFound, this.httpContext.Response.StatusCode);
			}
		}

		[TestMethod]
		public void Invoke500WritesInternalErrorAndLogsResult()
		{
			this.response.StatusCode = StatusCodes.Status500InternalServerError;
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

			this.middleware.Invoke(this.httpContext).Wait();

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = "{\r\n  \"result\": null,\r\n  \"error\": {\r\n    \"code\": -32603,\r\n    \"message\": \"Internal error\"\r\n  }\r\n}";
				Assert.AreEqual(expected, reader.ReadToEnd());
				Assert.AreEqual(StatusCodes.Status500InternalServerError, this.httpContext.Response.StatusCode);
				base.AssertLog(this.RPCLogger, LogLevel.Error, "Internal error while calling RPC Method");
			}
		}

		[TestMethod]
		public void InvokeThrowsUnhandledExceptionWritesInternalErrorAndLogsResult()
		{
			this.delegateContext.Setup(d => d(It.IsAny<DefaultHttpContext>()))
				.Throws(new InvalidOperationException("Operation not valid."));
			this.SetupValidAuthorization();
			this.InitializeFeatureContext();

			this.middleware.Invoke(this.httpContext).Wait();

			this.httpContext.Response.Body.Position = 0;
			using (var reader = new StreamReader(this.httpContext.Response.Body))
			{
				var expected = "{\r\n  \"result\": null,\r\n  \"error\": {\r\n    \"code\": -32603,\r\n    \"message\": \"Internal error\"\r\n  }\r\n}";
				Assert.AreEqual(expected, reader.ReadToEnd());
				Assert.AreEqual(StatusCodes.Status200OK, this.httpContext.Response.StatusCode);
				base.AssertLog<InvalidOperationException>(this.RPCLogger, LogLevel.Error, "Operation not valid.", "Internal error while calling RPC Method");
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