using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.Api.Controllers
{
    /// <summary>
    /// Controller serving static pages for the general node dashboard
    /// </summary>
    [Route("api/[controller]")]
    public class DashboardController : Controller
    {
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardController"/> class.
        /// </summary>
        public DashboardController(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Serves up the requested file from the embedded resources
        /// </summary>
        /// <returns>Web response with text content</returns>
        [HttpGet]
        [Route("static/{file_name}")]
        public ActionResult Static(string file_name)
        {
            var assembly = Assembly.GetEntryAssembly();
            assembly = Assembly.Load(new AssemblyName("Stratis.Bitcoin.Features.Api"));
            var resourceStream = assembly.GetManifestResourceStream("Stratis.Bitcoin.Features.Api.dashboard_static_files." + file_name);
            using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
            {
                if (file_name.EndsWith(".html")) {
                    return Content(reader.ReadToEnd(), "text/html");
                }
                else
                {
                    return Content(reader.ReadToEnd());
                }
            }
        }

    }
}
