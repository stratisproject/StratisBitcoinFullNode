using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps.Tests.AppStore
{
    public class FakeAppsFileService : IAppsFileService
    {
        public FakeAppsFileService()
        {
            this.StratisAppsFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public string StratisAppsFolderPath { get; }

        public IEnumerable<FileInfo> GetStratisAppFileInfos()
        {
            return new DirectoryInfo(this.StratisAppsFolderPath).GetFiles("*.dll", SearchOption.TopDirectoryOnly);
        }

        public IEnumerable<Type> GetTypesOfStratisApps(string stratisAppAssemblyPath)
        {
            return Assembly.LoadFrom(stratisAppAssemblyPath).GetTypes();
        }
    }
}
