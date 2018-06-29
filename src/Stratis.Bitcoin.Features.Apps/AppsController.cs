using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    [Route("api/[controller]")]
    public class AppsController : Controller
    {
        private readonly IAppsStore appsStore;

        public AppsController(IAppsStore appsStore)
        {
            this.appsStore = appsStore;
        }

        

    }
}
