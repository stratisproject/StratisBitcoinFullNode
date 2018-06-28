using System.IO;
using System.Reflection;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public abstract class StratisAppBase : IStratisApp
    {
        protected StratisAppBase(string displayName)
        {
            this.DisplayName = displayName;
            this.Location = Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location);
            this.WebRoot = Path.Combine(this.Location, "wwwroot");
        }

        public string DisplayName { get; }
        public string Location { get; }
        public string WebRoot { get; }
    }
}
