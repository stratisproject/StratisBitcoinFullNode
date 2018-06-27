using System;
using System.Collections.Generic;
using System.IO;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsFileService
    {
        bool DirectoryExists(string path);

        IEnumerable<FileInfo> GetAppFiles(string path);

        IEnumerable<Type> GetAppTypes(string assemblyPath);

        IObservable<string> WatchForNewFiles(string path);
    }
}
