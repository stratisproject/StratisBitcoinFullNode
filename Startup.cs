using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stratis.FederatedSidechains.AdminDashboard.Hubs;
using Stratis.FederatedSidechains.AdminDashboard.Services;
using Stratis.FederatedSidechains.AdminDashboard.Settings;

namespace Stratis.FederatedSidechains.AdminDashboard
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            //TODO: change this
            services.Configure<DefaultEndpointsSettings>(this.Configuration.GetSection("DefaultEndpoints"));

            services.AddDistributedMemoryCache();

            services.AddHostedService<FetchingBackgroundService>();

            services.AddMvc();

            services.AddSignalR();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            var cachePeriod = env.IsDevelopment() ? "600" : "604800";
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", $"public, max-age={cachePeriod}");
                }
            });

            app.UseSignalR(routes =>
            {
                routes.MapHub<DataUpdaterHub>("/ws-updater");
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
