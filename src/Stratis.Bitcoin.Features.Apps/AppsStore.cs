using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsStore : IAppsStore
    {
        private readonly object _lock = new object();
        private readonly ILogger logger;
        private List<IStratisApp> stratisApps;
        private readonly IAppsFileService appsFileService;

        public AppsStore(ILoggerFactory loggerFactory, IAppsFileService appsFileService)
        {
            this.appsFileService = appsFileService;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }       

        public IObservable<IReadOnlyCollection<IStratisApp>> GetApplications()
        {
            return Observable.Create<IReadOnlyCollection<IStratisApp>>(x =>
            {
                var apps = new List<IStratisApp>();
                lock (this._lock)
                {
                    if (this.stratisApps == null)
                    {
                        this.stratisApps = new List<IStratisApp>();
                        Load();
                    }

                    apps.AddRange(this.stratisApps);
                }

                x.OnNext(apps);

                return Disposable.Empty;
            });
        }

        private void Load()
        {
            Debug.Assert(this.stratisApps.IsEmpty());

            var apps = this.appsFileService.GetStratisAppFileInfos()
                            .SelectMany(x => CreateApps(this.appsFileService.GetTypesOfStratisApps(x.FullName))).ToList();
            if (apps.IsEmpty())
                this.logger?.LogWarning($"No Stratis apps found at or below {this.appsFileService.StratisAppsFolderPath}");

            this.stratisApps.AddRange(apps);

            LogApps(apps);
        }

        private static List<IStratisApp> CreateApps(IEnumerable<Type> types)
        {
            return types.Where(x => x.IsClass && x.BaseType == typeof(StratisAppBase))
                .Select(Activator.CreateInstance)
                .Cast<IStratisApp>()
                .ToList();
        }

        private void LogApps(List<IStratisApp> apps) => apps.ForEach(x => this.logger?.LogInformation($"Application loaded: {x}"));
    }
}
