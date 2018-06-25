using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppStore
    {
        IObservable<IReadOnlyCollection<IStratisApp>> GetApplications();

        //IObservable<IStratisApp> NewStream { get; }

        //IObservable<IStratisApp> RemovedStream { get; }
    }
}
