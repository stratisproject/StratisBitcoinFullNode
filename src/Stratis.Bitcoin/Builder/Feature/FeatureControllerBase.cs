using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Builder.Feature
{
    /// <summary>
    /// Base RestAPI Controller class for features.
    /// Has boilerplate code for consistent request validation, handling and mapping of errors without the need to
    /// duplicate lots of code.
    /// </summary>
    public abstract class FeatureControllerBase : Controller
    {
        public readonly ILogger Logger;

        protected FeatureControllerBase(ILogger logger)
        {
            this.Logger = logger;
        }

        protected async Task<IActionResult> Execute<TRequest>(TRequest request, CancellationToken token,
            Func<TRequest, CancellationToken, Task<IActionResult>> action, bool checkModelState = true)
        {
            if (checkModelState && !this.ModelState.IsValid)
            {
                Guard.NotNull(request, nameof(request));
                this.Logger.LogTrace($"{nameof(request)}(-)[MODEL_STATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                return await action(request, token);
            }
            catch (FeatureException e)
            {
                this.Logger.LogError("Exception occurred: {0}", e.ToString());
                return e.MapToErrorResponse();
            }
            catch (Exception e)
            {
                this.Logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        protected Task<IActionResult> ExecuteAsAsync<TRequest>(TRequest request,
            CancellationToken cancellationToken,
            Func<TRequest, CancellationToken, IActionResult> action, bool checkModelState = true)
        {
            return this.Execute(request, cancellationToken, (req, token)
                => Task.Run(() => action(req, token), token), checkModelState);
        }
    }
}