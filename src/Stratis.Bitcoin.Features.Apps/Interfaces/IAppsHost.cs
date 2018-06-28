using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsHost
    {
        bool Host(IEnumerable<IStratisApp> stratisApps);
    }
}
