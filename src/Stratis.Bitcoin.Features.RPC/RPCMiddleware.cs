using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NBitcoin.DataEncoders;
using NBitcoin.RPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC
{
    public class RPCMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IRPCAuthorization authorization;
        private readonly ILogger logger;
        public RPCMiddleware(RequestDelegate next, IRPCAuthorization authorization, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(next, nameof(next));
            Guard.NotNull(authorization, nameof(authorization));

            this.next = next;
            this.authorization = authorization;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            Guard.NotNull(httpContext, nameof(httpContext));

            if(!this.Authorized(httpContext))
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            Exception ex = null;
            try
            {
                await this.next.Invoke(httpContext);
            }
            catch(Exception exx)
            {
                ex = exx;
            }

            if (ex is ArgumentException || ex is FormatException)
            {
                JObject response = CreateError(RPCErrorCode.RPC_MISC_ERROR, "Argument error: " + ex.Message);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if(ex is RPCServerException)
            {
                var rpcEx = (RPCServerException)ex;
                JObject response = CreateError(rpcEx.ErrorCode, ex.Message);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if(httpContext.Response?.StatusCode == 404)
            {
                JObject response = CreateError(RPCErrorCode.RPC_METHOD_NOT_FOUND, "Method not found");
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if(this.IsDependencyFailure(ex))
            {
                JObject response = CreateError(RPCErrorCode.RPC_METHOD_NOT_FOUND, ex.Message);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
            else if(httpContext.Response?.StatusCode == 500 || ex != null)
            {
                JObject response = CreateError(RPCErrorCode.RPC_INTERNAL_ERROR, "Internal error");
                httpContext.Response.ContentType = "application/json";
                this.logger.LogError(new EventId(0), ex, "Internal error while calling RPC Method");
                await httpContext.Response.WriteAsync(response.ToString(Formatting.Indented));
            }
        }

        private bool IsDependencyFailure(Exception ex)
        {
            var invalidOp = ex as InvalidOperationException;
            if(invalidOp == null)
                return false;
            return invalidOp.Source.Equals("Microsoft.Extensions.DependencyInjection.Abstractions", StringComparison.Ordinal);
        }

        private bool Authorized(HttpContext httpContext)
        {
            if(!this.authorization.IsAuthorized(httpContext.Connection.RemoteIpAddress))
                return false;
            StringValues auth;
            if(!httpContext.Request.Headers.TryGetValue("Authorization", out auth) || auth.Count != 1)
                return false;
            var splittedAuth = auth[0].Split(' ');
            if(splittedAuth.Length != 2 ||
               splittedAuth[0] != "Basic")
                return false;

            try
            {
                var user = Encoders.ASCII.EncodeData(Encoders.Base64.DecodeData(splittedAuth[1]));
                if(!this.authorization.IsAuthorized(user))
                    return false;
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static JObject CreateError(RPCErrorCode code, string message)
        {
            JObject response = new JObject();
            response.Add("result", null);
            JObject error = new JObject();
            response.Add("error", error);
            error.Add("code", (int)code);
            error.Add("message", message);
            return response;
        }
    }
}
