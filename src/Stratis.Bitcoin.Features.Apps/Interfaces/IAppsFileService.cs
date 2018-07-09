using System.Collections.Generic;
using System.IO;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsFileService
    {
        string StratisAppsFolderPath { get; }

        IEnumerable<FileInfo> GetStratisAppConfigFileInfos();

        bool GetConfigurationFields(FileInfo stratisAppJson, out string displayName, out string webRoot);        
    }
}
