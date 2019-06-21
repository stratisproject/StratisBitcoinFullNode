using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Middleware to set the response Cache-Control to no-cache.
    /// </summary>
    public class NoCacheMiddleware
    {
        private readonly RequestDelegate _next;

        public NoCacheMiddleware(RequestDelegate next)
        {
            this._next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers["Cache-Control"] = "no-cache";

            await this._next(context);
        }
    }
}