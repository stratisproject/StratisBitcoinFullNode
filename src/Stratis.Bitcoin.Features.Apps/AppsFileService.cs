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
        private string stratisAppsFolderPath;
        public const string StratisAppFileName = "stratisApp.json";

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
                    throw new DirectoryNotFoundException($"No such directory '{value}'");
                
                this.stratisAppsFolderPath = value;
            }
        }

        public IEnumerable<FileInfo> GetStratisAppConfigFileInfos() =>
            new DirectoryInfo(this.StratisAppsFolderPath).GetFiles(StratisAppFileName, SearchOption.AllDirectories);
        
        public bool GetConfigurationFields(FileInfo stratisAppJson, out string displayName, out string webRoot)
        {
            displayName = webRoot = string.Empty;

            IConfigurationProvider provider = new ConfigurationBuilder()
                .SetBasePath(stratisAppJson.DirectoryName)
                .AddJsonFile(stratisAppJson.Name)
                .Build().Providers.First();

            return provider.TryGet("displayName", out displayName) &&
                   provider.TryGet("webRoot", out webRoot);
        }
    }
}
