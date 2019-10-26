using Microsoft.AspNetCore.Blazor.Browser.Rendering;
using Microsoft.AspNetCore.Blazor.Browser.Services;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Apps.Browser.Interfaces;
using Stratis.Bitcoin.Apps.Browser.Services;

namespace Stratis.Bitcoin.Apps.Browser
{
    public class Program
    {
        static void Main(string[] args)
        {
            var serviceProvider = new BrowserServiceProvider(services =>
            {
                services.Add(ServiceDescriptor.Singleton<IAppsService, AppsService>());
            });
            
            new BrowserRenderer(serviceProvider).AddComponent<App>("app");
        }
    }
}
