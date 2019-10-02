using System;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.SmartContracts.Core.State;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    [Route("swagger/[controller]")]
    public class ContractsController : Controller
    {
        private readonly ISwaggerProvider swaggerProvider;
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

        [Route("{address}")]
        [HttpGet]
        [SwaggerOperation(description: "test")]
        public IActionResult ContractSwaggerDoc(string address)
        {
            var doc = this.swaggerProvider.GetSwagger("contracts");
            var d = this.desc.ApiDescriptionGroups.Items;
            var o = this.options;
            var u = this.uiOptions.ConfigObject;
            return Ok(doc);
        }

    }
}
