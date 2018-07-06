using System;
using System.Collections.Generic;
using System.IO;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsFileService : IAppsFileService
    {
        private string stratisAppsFolderPath;

        public AppsFileService(DataFolder dataFolder)
        {
            this.StratisAppsFolderPath = dataFolder.ApplicationsPath;
        }

        public string StratisAppsFolderPath
        {
            get => this.stratisAppsFolderPath;
            private set
            {
                if (!Directory.Exists(value))
                    throw new Exception($"No such directory '{value}'");
                
                this.stratisAppsFolderPath = value;
            }
        }

        public IEnumerable<FileInfo> GetStratisAppConfigFileInfos() =>
            new DirectoryInfo(this.StratisAppsFolderPath).GetFiles("stratisApp.json", SearchOption.AllDirectories);
    }
}
