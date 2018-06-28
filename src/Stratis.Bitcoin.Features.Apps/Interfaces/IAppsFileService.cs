using System;
using System.Collections.Generic;
using System.IO;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsFileService
    {
        string StratisAppsFolderPath { get; }

        IEnumerable<FileInfo> GetStratisAppFileInfos();

        IEnumerable<Type> GetTypesOfStratisApps(string stratisAppAssemblyPath);
    }
}
