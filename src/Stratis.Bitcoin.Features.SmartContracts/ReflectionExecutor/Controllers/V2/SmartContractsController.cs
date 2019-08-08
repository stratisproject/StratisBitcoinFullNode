using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers.V2
{

    [ApiVersion("2")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class SmartContractsController : Controller
    {
        [Route("test")]
        [HttpGet]
        public IActionResult Test()
        {
            return this.Json("Working!");
        }
    }
}
