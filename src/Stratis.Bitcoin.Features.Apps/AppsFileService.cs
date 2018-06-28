using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsFileService : IAppsFileService
    {
        private const string SearchPattern = "*.dll";
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
                {
                    this.stratisAppsFolderPath = null;
                    throw new Exception($"No such directory '{value}'");
                }
                this.stratisAppsFolderPath = value;
            }
        }

        public IEnumerable<FileInfo> GetStratisAppFileInfos()
        {
            FolderPathMustBeSet();

            return new DirectoryInfo(this.stratisAppsFolderPath).GetFiles(SearchPattern, SearchOption.AllDirectories);
        }

        public IEnumerable<Type> GetTypesOfStratisApps(string stratisAppAssemblyPath)
        {
            FolderPathMustBeSet();

            return Assembly.LoadFrom(stratisAppAssemblyPath).GetTypes();
        }

        private void FolderPathMustBeSet()
        {
            Debug.Assert(!string.IsNullOrEmpty(this.stratisAppsFolderPath), $"{this.stratisAppsFolderPath} must be set");
        }
    }
}
