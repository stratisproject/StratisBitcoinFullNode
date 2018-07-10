using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class StratisApp : IStratisApp
    {
        public string DisplayName { get; set; }

        public string Location { get; set; }

        public string WebRoot { get; set; } = "wwwroot";

        public string Address { get; set; }

        public bool IsSinglePageApp { get; } = true;
    }
    
    public class StratisAppFactory : IStratisAppFactory
    {
        public IStratisApp New()
        {
            return new StratisApp();
        }
    }
}
