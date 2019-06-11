using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Routing;

namespace Stratis.FederatedSidechains.AdminDashboard.Filters
{
    /// <summary>
    /// The [Ajax] decorator prevent non-ajax request in the dashboard
    /// </summary>
    public class AjaxAttribute : ActionMethodSelectorAttribute
    {
        public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
        {
            return routeContext.HttpContext.Request.Headers["X-Requested-With"].Equals("XMLHttpRequest");
        }
    }
}
