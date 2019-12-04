using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Stratis.Bitcoin.Features.Api
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env, IFullNode fullNode)
        {
            this.fullNode = fullNode;

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            this.Configuration = builder.Build();
        }

        private IFullNode fullNode;

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(
                loggingBuilder =>
                {
                    loggingBuilder.AddConfiguration(this.Configuration.GetSection("Logging"));
                    loggingBuilder.AddConsole();
                    loggingBuilder.AddDebug();
                });

            // Add service and create Policy to allow Cross-Origin Requests
            services.AddCors
            (
                options =>
                {
                    options.AddPolicy
                    (
                        "CorsPolicy",

                        builder =>
                        {
                            var allowedDomains = new[] { "http://localhost", "http://localhost:4200" };

                            builder
                            .WithOrigins(allowedDomains)
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials();
                        }
                    );
                });

            // Add framework services.
            services
                .AddMvc(options =>
                {
                    options.Filters.Add(typeof(LoggingActionFilter));

                    ServiceProvider serviceProvider = services.BuildServiceProvider();
                    var apiSettings = (ApiSettings)serviceProvider.GetRequiredService(typeof(ApiSettings));
                    if (apiSettings.KeepaliveTimer != null)
                    {
                        options.Filters.Add(typeof(KeepaliveActionFilter));
                    }
                })
                // add serializers for NBitcoin objects
                .AddNewtonsoftJson(options => Utilities.JsonConverters.Serializer.RegisterFrontConverters(options.SerializerSettings))
                .AddControllers(this.fullNode.Services.Features, services);

            // Enable API versioning.
            // Note much of this is borrowed from https://github.com/microsoft/aspnet-api-versioning/blob/master/samples/aspnetcore/SwaggerSample/Startup.cs
            services.AddApiVersioning(options =>
            {
                // Our versions are configured to be set via URL path, no need to read from querystring etc.
                options.ApiVersionReader = new UrlSegmentApiVersionReader();

                // When no API version is specified, redirect to version 1.
                options.AssumeDefaultVersionWhenUnspecified = true;
            });

            // Add the versioned API explorer, which adds the IApiVersionDescriptionProvider service and allows Swagger integration.
            services.AddVersionedApiExplorer(
                options =>
                {
                    // Format the version as "'v'major[.minor][-status]"
                    options.GroupNameFormat = "'v'VVV";

                    // Substitute the version into the URLs in the swagger interface where we would otherwise see {version:apiVersion}
                    options.SubstituteApiVersionInUrl = true;
                });

            // Add custom Options injectable for Swagger. This is injected with the IApiVersionDescriptionProvider service from above.
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

            // Register the Swagger generator. This will use the options we injected just above.
            services.AddSwaggerGen();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory, IApiVersionDescriptionProvider provider)
        {
            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors("CorsPolicy");

            // Register this before MVC and Swagger.
            app.UseMiddleware<NoCacheMiddleware>();

            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS etc.)
            app.UseSwaggerUI(c =>
            {
                c.DefaultModelRendering(ModelRendering.Model);

                // Build a swagger endpoint for each discovered API version
                foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
                {
                    c.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                }
            });
        }
    }
}