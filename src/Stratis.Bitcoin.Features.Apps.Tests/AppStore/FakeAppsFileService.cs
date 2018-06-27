using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps.Tests.AppStore
{
    public class FakeAppsFileService : IAppsFileService
    {
        public bool DirectoryExists(string path)
        {
            return true;
        }

        public IEnumerable<FileInfo> GetAppFiles(string path)
        {
            var assemblyName = Path.GetFileName(Assembly.GetExecutingAssembly().CodeBase);
            return new DirectoryInfo(Directory.GetCurrentDirectory()).GetFiles(assemblyName, SearchOption.TopDirectoryOnly);
        }

        public IEnumerable<Type> GetAppTypes(string assemblyPath)
        {
            return Assembly.LoadFrom(assemblyPath).GetTypes();
        }

        public IObservable<string> WatchForNewFiles(string path)
        {
            return Observable.Empty<string>();
        }
    }
}
