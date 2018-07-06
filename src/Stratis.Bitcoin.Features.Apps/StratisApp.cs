namespace Stratis.Bitcoin.Features.Apps
{
    public class StratisApp
    {
        public string DisplayName { get; set; }

        public string Location { get; set; }

        public string WebRoot { get; set; } = "wwwroot";

        public string Address { get; set; }

        public bool IsSinglePageApp { get; } = true;
    }
}
