using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.RPC
{
    public class RPCMiddleware
    {
        private readonly RequestDelegate next;

        private readonly IRPCAuthorization authorization;

        private readonly ILogger logger;

        private readonly IHttpContextFactory httpContextFactory;

        public RPCMiddleware(RequestDelegate next, IRPCAuthorization authorization, ILoggerFactory loggerFactory, IHttpContextFactory httpContextFactory)
        {
            Guard.NotNull(next, nameof(next));
            Guard.NotNull(authorization, nameof(authorization));

            this.next = next;
            this.authorization = authorization;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.httpContextFactory = httpContextFactory;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            Guard.NotNull(httpContext, nameof(httpContext));

            if (!this.Authorized(httpContext))
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            Exception ex = null;
            try
            {
                string request = await this.ReadRequestAsync(httpContext.Request).ConfigureAwait(false);

                this.logger.LogDebug("RPC request: {0}", string.IsNullOrEmpty(request) ? request : JToken.Parse(request).ToString(Formatting.Indented));

                JToken token = string.IsNullOrEmpty(request) ? null : JToken.Parse(request);

                if (token is JArray)
                {
                    // Batch request, invoke each request and accumulate responses.
                    await this.InvokeAsyncBatchAsync(httpContext, token as JArray);
                }
                else
                {
                    // Single request, invoke the request.
                    await this.next.Invoke(httpContext);
                }
            }
            catch (Exception exx)
            {
                ex = exx;
            }

            await this.HandleRpcInvokeExceptionAsync(httpContext, ex);
        }

        [NoTrace]
        private async Task<string> ReadRequestAsync(HttpRequest request)
        {
            if (request.ContentLength == null || request.ContentLength == 0)
            {
                return string.Empty;
            }

            // Allows streams to be read multiple times.
            request.EnableRewind();

            // Read the request.
            byte[] requestBuffer = new byte[request.ContentLength.Value];
            await request.Body.ReadAsync(requestBuffer, 0, requestBuffer.Length).ConfigureAwait(false);

            string requestBody = Encoding.UTF8.GetString(requestBuffer);

            request.Body.Position = 0;

            return requestBody;
        }

        private async Task HandleRpcInvokeExceptionAsync(HttpContext httpContext, Exception ex)
        {
            if (ex is ArgumentException || ex is FormatException)
            {
                JObject response = CreateError(RPCErrorCode.RPC_MISC_ERROR, "Argument error: " + ex.Message);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if (ex is ConfigurationException)
            {
                JObject response = CreateError(RPCErrorCode.RPC_INTERNAL_ERROR, ex.Message);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if (ex is RPCServerException)
            {
                var rpcEx = (RPCServerException)ex;
                JObject response = CreateError(rpcEx.ErrorCode, ex.Message);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if (httpContext.Response?.StatusCode == 404)
            {
                JObject response = CreateError(RPCErrorCode.RPC_METHOD_NOT_FOUND, "Method not found");
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if (this.IsDependencyFailure(ex))
            {
                JObject response = CreateError(RPCErrorCode.RPC_METHOD_NOT_FOUND, ex.Message);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if (httpContext.Response?.StatusCode == 500 || ex != null)
            {
                JObject response = CreateError(RPCErrorCode.RPC_INTERNAL_ERROR, "Internal error");
                httpContext.Response.ContentType = "application/json";
                this.logger.LogError(new EventId(0), ex, "Internal error while calling RPC Method");
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
        }

        /// <summary>
        /// Invokes batch request.
        /// </summary>
        /// <param name="httpContext">Source batch request context.</param>
        /// <param name="requests">Array of requests.</param>
        private async Task InvokeAsyncBatchAsync(HttpContext httpContext, JArray requests)
        {
            JArray responses = new JArray();
            foreach (JObject requestObj in requests)
            {
                var contextFeatures = new FeatureCollection(httpContext.Features);

                StringBuilder requestStringBuilder = new StringBuilder();
                await requestObj.WriteToAsync(new JsonTextWriter(new StringWriter(requestStringBuilder)));
                var requestFeature = new HttpRequestFeature()
                {
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(requestStringBuilder.ToString())),
                    Headers = httpContext.Request.Headers,
                    Method = httpContext.Request.Method,
                    Protocol = httpContext.Request.Protocol,
                    Scheme = httpContext.Request.Scheme,
                    QueryString = httpContext.Request.QueryString.Value
                };
                contextFeatures.Set<IHttpRequestFeature>(requestFeature);

                var responseMemoryStream = new MemoryStream();
                var responseFeature = new HttpResponseFeature()
                {
                    Body = responseMemoryStream
                };
                contextFeatures.Set<IHttpResponseFeature>(responseFeature);

                contextFeatures.Set<IHttpRequestLifetimeFeature>(new HttpRequestLifetimeFeature());

                var context = this.httpContextFactory.Create(contextFeatures);

                try
                {
                    await this.next.Invoke(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await this.HandleRpcInvokeExceptionAsync(context, ex);
                }

                responseMemoryStream.Position = 0;
                var response = (responseMemoryStream.Length == 0) ? CreateError(RPCErrorCode.RPC_METHOD_NOT_FOUND, "Method not found") : await JObject.LoadAsync(new JsonTextReader(new StreamReader(responseMemoryStream)));
                responses.Add(response);
            }

            // Update the response with the array of responses.
            using (StreamWriter streamWriter = new StreamWriter(httpContext.Response.Body))
            using (JsonTextWriter textWriter = new JsonTextWriter(streamWriter))
            {
                await responses.WriteToAsync(textWriter);
            }
        }

        private bool IsDependencyFailure(Exception ex)
        {
            var invalidOp = ex as InvalidOperationException;
            if (invalidOp == null)
                return false;
            return invalidOp.Source.Equals("Microsoft.Extensions.DependencyInjection.Abstractions", StringComparison.Ordinal);
        }

        private bool Authorized(HttpContext httpContext)
        {
            IPAddress ip = httpContext.Connection.RemoteIpAddress;

            if (!this.authorization.IsAuthorized(ip))
            {
                this.logger.LogWarning("IP '{0}' not authorised.", ip);
                return false;
            }

            if (!httpContext.Request.Headers.TryGetValue("Authorization", out StringValues auth) || auth.Count != 1)
            {
                this.logger.LogWarning("No 'Authorization' header found.");
                return false;
            }

            string[] splittedAuth = auth[0].Split(' ');
            if (splittedAuth.Length != 2 || splittedAuth[0] != "Basic")
            {
                this.logger.LogWarning("Basic access authentication must be used when accessing RPC.");
                return false;
            }

            try
            {
                string user = Encoders.ASCII.EncodeData(Encoders.Base64.DecodeData(splittedAuth[1]));

                if (!this.authorization.IsAuthorized(user))
                {
                    this.logger.LogWarning("User with credentials '{0}' is not authorised.", user);
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred during authorisation: {0}.", ex.Message);
                return false;
            }

            return true;
        }

        private static JObject CreateError(RPCErrorCode code, string message)
        {
            var response = new JObject();
            response.Add("result", null);
            var error = new JObject();
            response.Add("error", error);
            error.Add("code", (int)code);
            error.Add("message", message);
            return response;
        }
    }
}
