using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Routing;

namespace Stratis.FederatedSidechains.AdminDashboard.Filters
{
    public class AjaxAttribute : ActionMethodSelectorAttribute
    {
        public string HttpVerb { get; set; }

        public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
        {
            return routeContext.HttpContext.Request.Headers["X-Requested-With"].Equals("XMLHttpRequest");
        }
    }
}
