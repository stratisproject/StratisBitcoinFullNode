using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsFileService : IAppsFileService
    {        
        public const string StratisAppFileName = "stratisApp.json";

        public AppsFileService(DataFolder dataFolder)
        {
            if (!Directory.Exists(dataFolder.ApplicationsPath))
                throw new DirectoryNotFoundException($"No such directory '{dataFolder.ApplicationsPath}'");

            this.StratisAppsFolderPath = dataFolder.ApplicationsPath;
        }

        public string StratisAppsFolderPath { get; }

        public IEnumerable<FileInfo> GetStratisAppConfigFileInfos() =>
            new DirectoryInfo(this.StratisAppsFolderPath).GetFiles(StratisAppFileName, SearchOption.AllDirectories);

        public string GetConfigSetting(FileInfo stratisAppConfig, string settingName)
        {
            IConfigurationProvider provider = new ConfigurationBuilder()
                .SetBasePath(stratisAppConfig.DirectoryName)
                .AddJsonFile(stratisAppConfig.Name)
                .Build().Providers.First();
            
            provider.TryGet(settingName, out string value);

            return value;
        }        
    }
}
