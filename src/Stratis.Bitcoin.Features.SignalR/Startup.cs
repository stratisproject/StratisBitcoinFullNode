using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddSignalR();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseCors(builder => builder
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowAnyOrigin()
                .AllowCredentials());

            //todo: this currently always go to DefaultRoute
            var settings = (SignalRSettings)app.ApplicationServices.GetService(typeof(SignalRSettings));
            app.UseSignalR(routes => routes.MapHub<SignalRHub>($"/{settings?.HubRoute ?? SignalRSettings.DefaultSignalRHubRoute}"));
        }
    }
}
