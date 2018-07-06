using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsHost
    {
        void Host(IEnumerable<StratisApp> stratisApps);
    }
}
