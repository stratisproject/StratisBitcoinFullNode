using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    public class DynamicContractController : Controller
    {
        [Route("api/contract/{address}/method/{method}")]
        [HttpPost]
        public IActionResult CallMethod([FromRoute] string address, [FromRoute] string method)
        {
            string requestBody;
            using (StreamReader reader = new StreamReader(this.Request.Body, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            // TODO map request body to JSON object, extract transaction-related params, build new request model, then call the regular SC controller.

            // Map parameters to our contract object and try to invoke it.
            // This will need to proxy to the actual SC controller

            return Ok(requestBody);
        }

        [Route("api/contract/{address}/property/{property}")]
        [HttpGet]
        public IActionResult LocalCallProperty([FromRoute] string address, [FromRoute] string property)
        {
            string requestBody;

            using (StreamReader reader = new StreamReader(this.Request.Body, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            // TODO map to local call and return result.

            return Ok(requestBody);
        }
    }
}