using System.Collections.Generic;
using System.Linq;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Swagger Documentation filter for removing mining related methods from API documentation.
    /// </summary>
    class ProofOfStakeConsensusApiFilter : IDocumentFilter
    {
        public void Apply(SwaggerDocument swaggerDoc, DocumentFilterContext context)
        {
            var pathsToRemove = swaggerDoc.Paths
                .Where(pathItem => pathItem.Key.Contains("/api/Mining/"))
                .ToList();

            foreach (KeyValuePair<string, PathItem> item in pathsToRemove)
            {
                swaggerDoc.Paths.Remove(item.Key);
            }
        }
    }
}
