using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.TestFeature.Controllers
{

    /// <summary>
    /// Controller providing TestFeature operation
    /// </summary>
    [Route("api/[controller]")]
    public class TestFeatureController : Controller
    {
        private readonly ConcurrentChain chain;

        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFeatureController"/> class.
        /// </summary>
        /// <param name="chain">The chain.</param>
        public TestFeatureController(ILoggerFactory loggerFactory, ConcurrentChain chain)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chain = chain;
        }

        [HttpGet]
        [Route("static/{file_name}")]
        public ActionResult Static(string file_name)
        {
            var assembly = Assembly.GetEntryAssembly();
            assembly = Assembly.Load(new AssemblyName("Stratis.Bitcoin.Features.TestFeature"));
            var resourceStream = assembly.GetManifestResourceStream("Stratis.Bitcoin.Features.TestFeature.dashboard_static_files." + file_name); //this folder should be added as an embedded resource using glob pattern matching in the csproj
            using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
            {
                if (file_name.EndsWith(".html") || file_name.EndsWith(".htm"))
                {
                    return Content(reader.ReadToEnd(), "text/html");
                }
                else
                {
                    return Content(reader.ReadToEnd());
                }
            }
        }

        [HttpGet]
        [Route("test1")]
        public IActionResult Test1()
        {
            this.logger.LogInformation("TestFeature endpoint hit.");
            return this.Json(chain.ToString()); //height
        }

    }
}
