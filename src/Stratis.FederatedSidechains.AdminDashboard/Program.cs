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
            var sidechainNodeType = app.Option<string>("--nodetype <NODE>", "Specify the sidechain node type: 10K or 50K", CommandOptionType.SingleValue);
            var environmentType = app.Option<string>("--env <env>", "Specify environment type: testnet or mainnet", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                IWebHostBuilder webHostBuilder = WebHost.CreateDefaultBuilder(args);
                IWebHost webHost = webHostBuilder.UseStartup<Startup>().Build();
                webHost.Run();
            });

            app.Execute(args);
        }
    }
}
