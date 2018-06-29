using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsStore
    {        
        IObservable<IReadOnlyCollection<IStratisApp>> GetApplications();

        IObservable<IStratisApp> NewAppStream { get; }
    }
}
