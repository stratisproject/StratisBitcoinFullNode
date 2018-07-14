
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Features.Api;
using HandlebarsDotNet;

namespace Stratis.Bitcoin.Features.Dashboard.Controllers
{
    public class DashboardController : Controller
    {
        [HttpGet("/_plugins/{pluginName}")]
        public ContentResult Index(string pluginName)
        {
          
            var content = System.IO.File.ReadAllText(Path.Combine(DashboardApiSettings.NodeSettings.DataDir, $"wwwroot/plugins/{pluginName}/index.html"));
            System.Func<object, string> result = Handlebars.Compile(content);
            var html = result(new { pluginName });
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = html
            };
        }

    }
}
