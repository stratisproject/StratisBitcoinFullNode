using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.Apps.Tests
{
    public class AppsStoreTests
    {
        private readonly IAppsStore appsStore;
        private readonly List<FileInfo> fileInfos;

        public AppsStoreTests()
        {
            ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;

            var appsFileService = new Mock<IAppsFileService>();
            appsFileService.Setup(x => x.StratisAppsFolderPath).Returns(Directory.GetCurrentDirectory());
            
            this.fileInfos = new List<FileInfo>();            

            this.appsStore = new AppsStore(loggerFactory, appsFileService.Object);
        }

        public void Test_Applications_returns_applications_as_expected()
        {

        }     
    }   
}
