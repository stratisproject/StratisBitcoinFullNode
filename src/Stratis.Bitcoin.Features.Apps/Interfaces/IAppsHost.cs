using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsHost
    {
        void Host(IEnumerable<IStratisApp> stratisApps);

        IEnumerable<IStratisApp> HostedApps { get; }

        void Close();
    }
}
