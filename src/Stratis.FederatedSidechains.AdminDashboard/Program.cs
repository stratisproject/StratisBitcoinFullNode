using System;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stratis.FederatedSidechains.AdminDashboard.Settings;

namespace Stratis.FederatedSidechains.AdminDashboard
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "Stratis.FederatedSidechains.AdminDashboard",
                Description = "Stratis Federation Members Dashboard"
            };
            
            app.HelpOption(true);
            
            var mainchainportOption = app.Option<int>("--mainchainport <PORT>", "Specify the port that you want to use for the Main Chain", CommandOptionType.SingleValue);
            var sidechainportOption = app.Option<int>("--sidechainport <PORT>", "Specify the port that you want to use for the Side Chain", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                CreateWebHostBuilder(args, mainchainportOption, sidechainportOption).Build().Run();
            });

            app.Execute(args);
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args, CommandOption<int> mainchainportOption, CommandOption<int> sidechainportOption)
        {
            var webHostBuilder = WebHost.CreateDefaultBuilder(args);
            if(mainchainportOption.HasValue())
            {
                webHostBuilder.UseSetting("DefaultEndpoints:StratisNode", $"http://localhost:{mainchainportOption.Value()}");
            }
            if(sidechainportOption.HasValue())
            {
                webHostBuilder.UseSetting("DefaultEndpoints:SidechainNode", $"http://localhost:{sidechainportOption.Value()}");
            }
            return webHostBuilder.UseStartup<Startup>();
        }
    }
}
