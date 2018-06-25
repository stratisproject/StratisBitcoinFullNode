using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppStore : IAppStore
    {
        private readonly object _lock = new object();
        private readonly List<IStratisApp> apps = new List<IStratisApp>();

        public AppStore()
        {
            ReadFromDisk();
        }

        public IObservable<IStratisApp> GetApplications()
        {

        }

        private void ReadFromDisk()
        { 
        }
    }
}
