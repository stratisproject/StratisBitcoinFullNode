using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsStore
    {        
        IEnumerable<IStratisApp> Applications { get; }
    }
}
