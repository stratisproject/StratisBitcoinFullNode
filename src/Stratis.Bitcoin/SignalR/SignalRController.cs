using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.SignalR
{
    /// <summary>
    /// Provides methods to support SignalR clients of the full node.
    /// </summary>
    [Route("api/[controller]")]
    public class SignalRController : Controller
    {
        private readonly ISignalRService signalRService;

        public SignalRController(ISignalRService signalRService)
        {
            this.signalRService = signalRService;
        }

        [HttpGet]
        [Route("address")]
        public IActionResult Address() => this.Content(this.signalRService.Address.AbsoluteUri);
    }
}
