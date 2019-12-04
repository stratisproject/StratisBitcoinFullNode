using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(
                options =>
                {
                    options.AddPolicy(
                        "CorsPolicy",
                        builder =>
                        {
                            var allowedDomains = new[] { "http://localhost", "http://localhost:4200" };

                            builder
                                .WithOrigins(allowedDomains)
                                .AllowAnyMethod()
                                .AllowAnyHeader()
                                .AllowCredentials();
                        });
                });
            services.AddSignalR().AddNewtonsoftJsonProtocol(options =>
            {
                var settings = new JsonSerializerSettings();
                Serializer.RegisterFrontConverters(settings);
                options.PayloadSerializerSettings = settings;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider)
        {
            app.UseRouting();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("CorsPolicy");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<EventsHub>("/events-hub");
            });
        }
    }
}