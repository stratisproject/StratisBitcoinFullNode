using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// An asynchronous action filter whose role is to log details from the Http requests to the API.
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter" />
    public class LoggingActionFilter : IAsyncActionFilter
    {
        private readonly ILogger logger;

        public LoggingActionFilter(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger("api.request.logger");
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            HttpRequest request = context.HttpContext.Request;

            // get the body
            string body = string.Empty;
            IDictionary<string, object> arguments = context.ActionArguments;
            if (request.ContentLength != null && arguments != null && arguments.Any())
            {
                body = string.Join(Environment.NewLine, arguments.Values);
            }

            this.logger.LogDebug($"Received {request.Method} {request.GetDisplayUrl()}. Body: '{body}'");
            await next();
        }
    }
}
