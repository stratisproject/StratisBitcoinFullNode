using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Stratis.Bitcoin.Controllers;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Swagger Documentation filter for removing staking related methods from API documentation.
    /// </summary>
    public class HideWhenProofOfWork : IDocumentFilter
    {
        public void Apply(SwaggerDocument swaggerDoc, DocumentFilterContext context)
        {
            // First get a list of controllers with the proof of stake attribute
            foreach (ApiDescription apiDescription in context.ApiDescriptions)
            {
                var controllerActionDescriptor = apiDescription.ActionDescriptor as ControllerActionDescriptor;
                if (!controllerActionDescriptor.ControllerTypeInfo.GetCustomAttributes(typeof(ProofOfStakeAttribute), true).Any())
                    continue;

                // Next get the path to remove
                List<KeyValuePair<string, PathItem>> pathsToRemove = swaggerDoc.Paths
                    .Where(pathItem => pathItem.Key.Contains(controllerActionDescriptor.ControllerName.Replace("Controller", "")))
                    .ToList();

                // Removing the selected paths from swagger documentation
                foreach (KeyValuePair<string, PathItem> item in pathsToRemove)
                {
                    swaggerDoc.Paths.Remove(item.Key);
                }
            }
        }
    }
}
