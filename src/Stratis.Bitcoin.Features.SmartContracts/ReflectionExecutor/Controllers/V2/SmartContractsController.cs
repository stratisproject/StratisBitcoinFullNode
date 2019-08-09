using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers.V2
{

    [ApiVersion("2.0-dev")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class SmartContractsController : Controller
    {

        // Note that it's also possible to have v2 calls on older controllers. See https://github.com/Microsoft/aspnet-api-versioning/wiki/Versioning-via-the-URL-Path

        [Route("demo")]
        [HttpGet]
        public IActionResult Demo()
        {
            return this.Json("This endpoint demonstrates how easy it is to set up a version 2 API.");
        }
    }
}
