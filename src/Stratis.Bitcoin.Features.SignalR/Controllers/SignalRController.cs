using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR.Controllers
{
    /// <summary>
    /// Controller for connecting to SignalR.
    /// </summary>
    [Route("api/[controller]")]
    public class SignalRController : Controller
    {
        private readonly SignalRSettings signalRSettings;

        public SignalRController(SignalRSettings signalRSettings)
        {
            Guard.NotNull(signalRSettings, nameof(signalRSettings));

            this.signalRSettings = signalRSettings;
        }

        /// <summary>
        /// Returns SignalR Connection Info.
        /// </summary>
        /// <returns>Returns SignalR Connection Info as Json {SignalRUri,SignalPort}</returns>
        /// <response code="200">Returns connection info</response>
        [Route("getConnectionInfo")]
        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public IActionResult GetConnectionInfo()
        {
            return this.Json(new
            {
                this.signalRSettings.SignalRUri,
                this.signalRSettings.SignalPort
            });
        }
    }
}