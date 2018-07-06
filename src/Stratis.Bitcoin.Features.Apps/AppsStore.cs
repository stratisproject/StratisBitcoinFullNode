using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsStore : IAppsStore
    {
        private readonly ILogger logger;
        private readonly List<StratisApp> applications = new List<StratisApp>();
        private readonly IAppsFileService appsFileService;

        public AppsStore(ILoggerFactory loggerFactory, IAppsFileService appsFileService)
        {
            this.appsFileService = appsFileService;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.Load();
        }

        public IEnumerable<StratisApp> Applications => this.applications;

        private void Load()
        {
            IEnumerable<FileInfo> configs = this.appsFileService.GetStratisAppConfigFileInfos();
            var apps = configs.Select(this.CreateApp).Where(x => x != null).ToList();

            if (apps.IsEmpty())
            {
                this.logger.LogWarning(
                    $"No Stratis applications found at or below {this.appsFileService.StratisAppsFolderPath}");
                return;
            }

            this.applications.AddRange(apps);
        }

        private StratisApp CreateApp(FileInfo fileInfo)
        {
            try
            {
                IConfigurationProvider provider = new ConfigurationBuilder()
                    .SetBasePath(fileInfo.DirectoryName)
                    .AddJsonFile(fileInfo.Name)
                    .Build().Providers.First();

                provider.TryGet("displayName", out string displayName);
                provider.TryGet("webRoot", out string webRoot);

                var stratisApp = new StratisApp { DisplayName = displayName, Location = fileInfo.DirectoryName };
                if (!string.IsNullOrEmpty(webRoot))
                    stratisApp.WebRoot = webRoot;

                return stratisApp;
            }
            catch (Exception e)
            {
                this.logger.LogError($"Failed to create app : {e.Message}");   
                return null;
            }
        }
    }
}
