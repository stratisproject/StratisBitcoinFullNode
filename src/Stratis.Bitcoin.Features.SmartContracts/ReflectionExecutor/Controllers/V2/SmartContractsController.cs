using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers.V2
{

    [ApiVersion("2.0-alpha")]
    //[Route("api/[controller]")] TODO: Do we need this?
    public class SmartContractsController : Controller
    {
        public IActionResult Test()
        {
            return this.Json("Working!");
        }
    }
}
