using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsFileService : IAppsFileService
    {
        private const string SearchPattern = "*.dll";

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public IEnumerable<FileInfo> GetAppFiles(string path)
        {
            return new DirectoryInfo(path).GetFiles(SearchPattern, SearchOption.AllDirectories);
        }

        public IEnumerable<Type> GetAppTypes(string assemblyPath)
        {
            return Assembly.LoadFrom(assemblyPath).GetTypes();
        }

        public IObservable<string> WatchForNewFiles(string path)
        {
            return Observable.Create<string>(x =>
            {
                var watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    Filter = SearchPattern
                };
                watcher.Created += (_, args) => x.OnNext(args.FullPath);

                return Disposable.Create(() => watcher.Dispose());
            });
        }
    }
}
