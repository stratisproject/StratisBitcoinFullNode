using System;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Mvc.Filters;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// An asynchronous action filter whose role is to reset the keepalive counter.
    /// </summary>
    /// <seealso cref="IAsyncActionFilter" />
    public class KeepaliveActionFilter : IAsyncActionFilter
    {
        /// <summary> The keepalive object. </summary>
        private readonly Timer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeepaliveActionFilter"/> class.
        /// </summary>
        /// <param name="options"></param>
        public KeepaliveActionFilter(ApiFeatureOptions options)
        {
            if (options == null)
            {
                return;
            }

            this.timer = options.KeepaliveTimer;
        }

        /// <inheritdoc />
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // If keepalive is used, reset the timer.
            this.timer?.Reset();

            await next();
        }
    }
}
