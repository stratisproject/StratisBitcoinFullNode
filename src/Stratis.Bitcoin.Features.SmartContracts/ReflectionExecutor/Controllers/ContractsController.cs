using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    public class SwaggerUIContractListMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IReceiptRepository receiptRepository;
        private readonly SwaggerUIOptions config;

        public SwaggerUIContractListMiddleware(RequestDelegate next,
            IReceiptRepository receiptRepository,
            SwaggerUIOptions options)
        {
            this.next = next;
            this.receiptRepository = receiptRepository;
            this.config = options;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var urls = this.config.ConfigObject.Urls;

            var newUrls = new List<UrlDescriptor>(urls);

            

            foreach (UrlDescriptor url in newUrls.Where(u => u.Name.Contains("contract")))
            {
                url.Name = "Test";
            }

            this.config.ConfigObject.Urls = newUrls;

            await this.next.Invoke(httpContext);
        }
    }

    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "contracts", IgnoreApi = false)]
    public class ContractsController : Controller
    {
        private ISwaggerProvider swaggerProvider;
        private readonly IApiDescriptionGroupCollectionProvider desc;
        private readonly IStateRepositoryRoot stateRepository;
        private readonly SwaggerGeneratorOptions options;
        private readonly SwaggerUIOptions uiOptions;

        public ContractsController(ISwaggerProvider swaggerProvider, IApiDescriptionGroupCollectionProvider desc, IOptions<SwaggerGeneratorOptions> options, IOptions<SwaggerUIOptions> uiOptions, IStateRepositoryRoot stateRepository)
        {
            this.swaggerProvider = swaggerProvider;
            this.desc = desc;
            this.stateRepository = stateRepository;
            this.options = options.Value;
            this.uiOptions = uiOptions.Value;
        }

        [Route("{i}")]
        [HttpGet]
        [SwaggerOperation(description: "test")]
        public IActionResult ContractSwaggerDoc(int i)
        {
            var doc = this.swaggerProvider.GetSwagger("contracts");
            var d = this.desc.ApiDescriptionGroups.Items;
            var o = this.options;
            var u = this.uiOptions.ConfigObject;
            return Ok(doc);
        }

    }
}
