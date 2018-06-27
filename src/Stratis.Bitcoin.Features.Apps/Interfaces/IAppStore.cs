using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppStore
    {
        IObservable<IStratisApp> NewAppStream { get; }

        IObservable<IReadOnlyCollection<IStratisApp>> GetApplications();
    }
}
