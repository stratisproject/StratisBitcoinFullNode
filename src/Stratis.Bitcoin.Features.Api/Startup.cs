using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Swashbuckle.AspNetCore.Swagger;

namespace Stratis.Bitcoin.Features.Api
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            this.Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
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
            services.AddMvc(options =>
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
                .AddJsonOptions(options => Utilities.JsonConverters.Serializer.RegisterFrontConverters(options.SerializerSettings))
                .AddControllers(services);

            // Register the Swagger generator, defining one or more Swagger documents
            services.AddSwaggerGen(setup =>
            {
                setup.SwaggerDoc("v1", new Info { Title = "Stratis.Bitcoin.Api", Version = "v1" });

                //Set the comments path for the swagger json and ui.
                string basePath = PlatformServices.Default.Application.ApplicationBasePath;
                string apiXmlPath = Path.Combine(basePath, "Stratis.Bitcoin.Api.xml");
                string walletXmlPath = Path.Combine(basePath, "Stratis.Bitcoin.LightWallet.xml");

                if (File.Exists(apiXmlPath))
                {
                    setup.IncludeXmlComments(apiXmlPath);
                }

                if (File.Exists(walletXmlPath))
                {
                    setup.IncludeXmlComments(walletXmlPath);
                }

                setup.DescribeAllEnumsAsStrings();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(this.Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseCors("CorsPolicy");

            app.UseMvc();

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS etc.), specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.DefaultModelRendering(ModelRendering.Model);
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Stratis.Bitcoin.Api V1");
            });
        }
    }
}