using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppStore : IAppStore, IDisposable
    {
        private bool disposed;
        private readonly object _lock = new object();
        private readonly DataFolder dataFolder;
        private readonly ILogger logger;
        private List<IStratisApp> stratisApps;
        private FileSystemWatcher appsFolderWatcher;
        private readonly IAppsFileService appsFileService;
        private readonly Subject<IStratisApp> newAppStream = new Subject<IStratisApp>();

        public AppStore(DataFolder dataFolder, ILoggerFactory loggerFactory, IAppsFileService appsFileService)
        {
            this.dataFolder = dataFolder;
            this.appsFileService = appsFileService;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.NewAppStream = this.newAppStream.AsObservable();
        }

        public IObservable<IStratisApp> NewAppStream { get; }

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
                        Watch();
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

            var appsPath = this.dataFolder.ApplicationsPath;
            if (!this.appsFileService.DirectoryExists(appsPath))
            {
                this.logger?.LogWarning($"{appsPath} does not exist");
                return;
            }

            var apps = this.appsFileService.GetAppFiles(appsPath)
                            .SelectMany(x => CreateApps(this.appsFileService.GetAppTypes(x.FullName))).ToList();
            if (apps.IsEmpty())
                this.logger?.LogWarning($"No Stratis apps found at {appsPath}");

            this.stratisApps.AddRange(apps);

            LogApps(apps);
        }

        private void Watch()
        {
            if (this.stratisApps.IsEmpty())
                return;

            this.appsFileService.WatchForNewFiles(this.dataFolder.ApplicationsPath)
                .Subscribe(OnHandleCreatedFile);
        }

        private void OnHandleCreatedFile(string newPath)
        {
            List<IStratisApp> newApps = CreateApps(Assembly.LoadFrom(newPath).GetTypes());
            if (!newApps.Any())
                return;
            
            lock (this._lock)
                this.stratisApps.AddRange(newApps);

            newApps.ForEach(this.newAppStream.OnNext);
        }

        private static List<IStratisApp> CreateApps(IEnumerable<Type> types)
        {
            return types.Where(x => x.IsClass && x.GetInterface(nameof(IStratisApp)) != null)
                .Select(Activator.CreateInstance)
                .Cast<IStratisApp>()
                .ToList();
        }

        private void LogApps(List<IStratisApp> apps) => apps.ForEach(x => this.logger?.LogInformation($"Application loaded: {x}"));

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.appsFolderWatcher?.Dispose();
                this.appsFolderWatcher = null;
            }

            this.disposed = true;
        }
    }
}
