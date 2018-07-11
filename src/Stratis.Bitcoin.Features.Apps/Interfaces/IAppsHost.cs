using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsHost
    {
        IEnumerable<IStratisApp> HostedApps { get; }

        void Host(IEnumerable<IStratisApp> stratisApps);        

        void Close();
    }
}
