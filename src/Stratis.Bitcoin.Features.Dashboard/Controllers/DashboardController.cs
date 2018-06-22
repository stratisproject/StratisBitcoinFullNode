
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Features.Api;

namespace Stratis.Bitcoin.Features.Dashboard.Controllers
{
    public class DashboardController : Controller
    {

        [HttpGet("/")]
        public ContentResult Index()
        {
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = System.IO.File.ReadAllText(Path.Combine(DashboardApiSettings.GetRootDirectory(), "wwwroot/index.html"))
            };
        }

    }
}
