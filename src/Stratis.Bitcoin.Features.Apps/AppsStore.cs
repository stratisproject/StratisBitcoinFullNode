using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsStore : IAppsStore
    {
        private readonly ILogger logger;
        private readonly List<IStratisApp> applications = new List<IStratisApp>();
        private readonly IAppsFileService appsFileService;
        private readonly IStratisAppFactory appFactory;

        public AppsStore(ILoggerFactory loggerFactory, IAppsFileService appsFileService, IStratisAppFactory appFactory)
        {
            this.appsFileService = appsFileService;
            this.appFactory = appFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.Load();
        }

        public IEnumerable<IStratisApp> Applications => this.applications;

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

        private IStratisApp CreateApp(FileInfo fileInfo)
        {
            try
            {
                var displayName = this.appsFileService.GetConfigSetting(fileInfo, "displayName");
                var webRoot = this.appsFileService.GetConfigSetting(fileInfo, "webRoot");

                IStratisApp stratisApp = this.appFactory.New();
                stratisApp.DisplayName = displayName;
                stratisApp.Location = fileInfo.DirectoryName;

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
