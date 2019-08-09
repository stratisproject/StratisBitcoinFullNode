using System.IO;
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
        private const string ApiXmlFilename = "Stratis.Bitcoin.Api.xml";
        private const string WalletXmlFilename = "Stratis.Bitcoin.LightWallet.xml";

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

            //Set the comments path for the swagger json and ui.
            string basePath = PlatformServices.Default.Application.ApplicationBasePath;
            string apiXmlPath = Path.Combine(basePath, ApiXmlFilename);
            string walletXmlPath = Path.Combine(basePath, WalletXmlFilename);

            if (File.Exists(apiXmlPath))
            {
                options.IncludeXmlComments(apiXmlPath);
            }

            if (File.Exists(walletXmlPath))
            {
                options.IncludeXmlComments(walletXmlPath);
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
