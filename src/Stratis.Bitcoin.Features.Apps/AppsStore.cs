using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.Apps
{
    /// <summary>
    /// Responsible for storing StratisApps as read from the current running Stratis folder.
    /// </summary>
    public class AppsStore : IAppsStore
    {
        private readonly ILogger logger;
        private List<IStratisApp> applications;
        private readonly DataFolder dataFolder;
        private const string ConfigFileName = "stratisApp.json";

        public AppsStore(ILoggerFactory loggerFactory, DataFolder dataFolder)
        {
            this.dataFolder = dataFolder;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public IEnumerable<IStratisApp> Applications
        {
            get
            {
                this.Load();

                return this.applications;
            }
        }

        private void Load()
        {
            try
            {
                if (this.applications != null)
                    return;

                this.applications = new List<IStratisApp>();

                FileInfo[] fileInfos = new DirectoryInfo(this.dataFolder.ApplicationsPath)
                    .GetFiles(ConfigFileName, SearchOption.AllDirectories);

                IEnumerable<IStratisApp> apps = fileInfos.Select(x => new FileStorage<StratisApp>(x.DirectoryName))
                                                         .Select(this.CreateAppInstance);

                this.applications.AddRange(apps.Where(x => x != null));

                if (this.applications.IsEmpty())
                    this.logger.LogWarning("No Stratis applications found at or below '{0}'.", this.dataFolder.ApplicationsPath);
            }
            catch (Exception e)
            {
                this.logger.LogError("Failed to load Stratis apps :{0}", e.Message);
            }
        }

        private IStratisApp CreateAppInstance(FileStorage<StratisApp> fileStorage)
        {
            try
            {
                StratisApp stratisApp = fileStorage.LoadByFileName(ConfigFileName);
                stratisApp.Location = fileStorage.FolderPath;
                return stratisApp;
            }
            catch (Exception e)
            {
                this.logger.LogError("Failed to create app :{0}", e.Message);
                return null;
            }
        }
    }
}
