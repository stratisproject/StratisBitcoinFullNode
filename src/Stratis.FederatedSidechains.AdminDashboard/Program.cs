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
                CreateWebHostBuilder(args, mainchainportOption, sidechainportOption, sidechainNodeType, environmentType).Build().Run();
            });

            app.Execute(args);
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args, CommandOption<int> mainchainportOption, CommandOption<int> sidechainportOption, CommandOption<string> sidechainNodeType, CommandOption<string> nodeEnv)
        {
            IWebHostBuilder webHostBuilder = WebHost.CreateDefaultBuilder(args);
            webHostBuilder.UseSetting("DefaultEndpoints:EnvType", NodeEnv.MainNet);
            if (mainchainportOption.HasValue())
                webHostBuilder.UseSetting("DefaultEndpoints:StratisNode", $"http://localhost:{mainchainportOption.Value()}");
            if(sidechainportOption.HasValue())
                webHostBuilder.UseSetting("DefaultEndpoints:SidechainNode", $"http://localhost:{sidechainportOption.Value()}");
            if (sidechainNodeType.HasValue() && !string.IsNullOrEmpty(sidechainNodeType.Value()))
            {
                var nodeType =
                    sidechainNodeType.Value().Contains("50", StringComparison.OrdinalIgnoreCase) || sidechainNodeType
                        .Value().Contains("fifty", StringComparison.OrdinalIgnoreCase)
                        ? NodeTypes.FiftyK
                        : NodeTypes.TenK;
                webHostBuilder.UseSetting("DefaultEndpoints:SidechainNodeType", nodeType);
            }

            if (nodeEnv.HasValue() && !string.IsNullOrEmpty(nodeEnv.Value()))
            {
                var envType = nodeEnv.Value().Contains("testnet", StringComparison.OrdinalIgnoreCase)
                    ? NodeEnv.TestNet
                    : NodeEnv.MainNet;
                webHostBuilder.UseSetting("DefaultEndpoints:EnvType", envType);
            }

            return webHostBuilder.UseStartup<Startup>();
        }
    }
}
