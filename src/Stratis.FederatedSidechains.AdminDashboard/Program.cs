using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace Stratis.FederatedSidechains.AdminDashboard
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls("http://localhost:1989")   //TODO: temporary port
                .UseStartup<Startup>();
    }
}
