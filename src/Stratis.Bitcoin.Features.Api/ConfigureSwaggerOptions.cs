using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Configures the Swagger generation options.
    /// </summary>
    /// <remarks>This allows API versioning to define a Swagger document per API version after the
    /// <see cref="IApiVersionDescriptionProvider"/> service has been resolved from the service container.
    /// Adapted from https://github.com/microsoft/aspnet-api-versioning/blob/master/samples/aspnetcore/SwaggerSample/ConfigureSwaggerOptions.cs.
    /// </remarks>
    public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private static readonly string[] ApiXmlDocuments = new string[]
        {
            "Stratis.Bitcoin.xml",
            "Stratis.Bitcoin.Features.BlockStore.xml",
            "Stratis.Bitcoin.Features.ColdStaking.xml",
            "Stratis.Bitcoin.Features.Consensus.xml",
            "Stratis.Bitcoin.Features.PoA.xml",
            "Stratis.Bitcoin.Features.MemoryPool.xml",
            "Stratis.Bitcoin.Features.Miner.xml",
            "Stratis.Bitcoin.Features.Notifications.xml",
            "Stratis.Bitcoin.Features.RPC.xml",
            "Stratis.Bitcoin.Features.SignalR.xml",
            "Stratis.Bitcoin.Features.SmartContracts.xml",
            "Stratis.Bitcoin.Features.Wallet.xml",
            "Stratis.Bitcoin.Features.WatchOnlyWallet.xml",
            "Stratis.Features.Diagnostic.xml",
            "Stratis.Features.FederatedPeg.xml"
        };

        private readonly IApiVersionDescriptionProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureSwaggerOptions"/> class.
        /// </summary>
        /// <param name="provider">The <see cref="IApiVersionDescriptionProvider">provider</see> used to generate Swagger documents.</param>
        public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
        {
            this.provider = provider;
        }

        /// <inheritdoc />
        public void Configure(SwaggerGenOptions options)
        {
            // Add a swagger document for each discovered API version
            foreach (ApiVersionDescription description in this.provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
            }

            // Includes XML comments in Swagger documentation 
            string basePath = PlatformServices.Default.Application.ApplicationBasePath;
            foreach (string xmlPath in ApiXmlDocuments.Select(xmlDocument => Path.Combine(basePath, xmlDocument)))
            {
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            }

            options.DescribeAllEnumsAsStrings();
        }

        static Info CreateInfoForApiVersion(ApiVersionDescription description)
        {
            var info = new Info()
            {
                Title = "Stratis Node API",
                Version = description.ApiVersion.ToString(),
                Description = "Access to the Stratis Node's core features."
            };

            if (info.Version.Contains("dev"))
            {
                info.Description += " This version of the API is in development and subject to change. Use an earlier version for production applications.";
            }

            if (description.IsDeprecated)
            {
                info.Description += " This API version has been deprecated.";
            }

            return info;
        }
    }
}
