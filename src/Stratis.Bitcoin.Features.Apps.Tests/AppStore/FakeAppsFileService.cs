using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IEnumerable<FileInfo> GetStratisAppConfigFileInfos()
        {
            return Enumerable.Empty<FileInfo>();
        }
    }
}
