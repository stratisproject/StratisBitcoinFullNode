using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.Apps.Tests
{
    public class AppsStoreTests
    {        
        private readonly ILoggerFactory loggerFactory;
        private readonly StratisAppFactory appFactory;
        private readonly Mock<IAppsFileService> appsFileService;

        public AppsStoreTests()
        {
            this.loggerFactory = new Mock<ILoggerFactory>().Object;
            this.appsFileService = new Mock<IAppsFileService>();
            this.appFactory = new StratisAppFactory();
        }

        [Fact]
        public void Test_Applications_returns_an_application()
        {            
            var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            this.appsFileService.Setup(x => x.StratisAppsFolderPath).Returns(Directory.GetCurrentDirectory());
            this.appsFileService.Setup(x => x.GetStratisAppConfigFileInfos()).Returns(new List<FileInfo>{fileInfo});
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "displayName")).Returns(It.IsAny<string>());
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "webRoot")).Returns(It.IsAny<string>());

            var store = new AppsStore(this.loggerFactory, this.appsFileService.Object, this.appFactory);

            Assert.Single(store.Applications);
        }

        [Fact]
        public void Test_Applications_returns_applications()
        {            
            var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            this.appsFileService.Setup(x => x.StratisAppsFolderPath).Returns(Directory.GetCurrentDirectory());
            this.appsFileService.Setup(x => x.GetStratisAppConfigFileInfos()).Returns(new List<FileInfo> { fileInfo, fileInfo });
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "displayName")).Returns("app");
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "webRoot")).Returns("root");

            var store = new AppsStore(this.loggerFactory, this.appsFileService.Object, this.appFactory);

            Assert.Equal(2, store.Applications.Count());
        }

        [Fact]
        public void Test_DisplayName_is_set_on_returned_StratisApp()
        {            
            var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            this.appsFileService.Setup(x => x.StratisAppsFolderPath).Returns(Directory.GetCurrentDirectory());
            this.appsFileService.Setup(x => x.GetStratisAppConfigFileInfos()).Returns(new List<FileInfo> { fileInfo });
            const string displayName = "myapplication";
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "displayName")).Returns(displayName);
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "webRoot")).Returns("root");

            var store = new AppsStore(this.loggerFactory, this.appsFileService.Object, this.appFactory);

            Assert.Equal(displayName, store.Applications.First().DisplayName);
        }

        [Fact]
        public void Test_WebRoot_is_set_on_returned_StratisApp()
        {            
            var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            this.appsFileService.Setup(x => x.StratisAppsFolderPath).Returns(Directory.GetCurrentDirectory());
            this.appsFileService.Setup(x => x.GetStratisAppConfigFileInfos()).Returns(new List<FileInfo> { fileInfo });
            const string webRoot = "myroot";
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "displayName")).Returns("app");
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "webRoot")).Returns(webRoot);

            var store = new AppsStore(this.loggerFactory, this.appsFileService.Object, this.appFactory);

            Assert.Equal(webRoot, store.Applications.First().WebRoot);
        }

        [Fact]
        public void Test_WebRoot_is_set_to_wwwroot_by_default()
        {
            var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            this.appsFileService.Setup(x => x.StratisAppsFolderPath).Returns(Directory.GetCurrentDirectory());
            this.appsFileService.Setup(x => x.GetStratisAppConfigFileInfos()).Returns(new List<FileInfo> { fileInfo });            
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "displayName")).Returns("app");            

            var store = new AppsStore(this.loggerFactory, this.appsFileService.Object, this.appFactory);

            Assert.Equal("wwwroot", store.Applications.First().WebRoot);
        }

        [Fact]
        public void Test_Location_is_set_on_returned_StratisApp()
        {            
            string location = Assembly.GetExecutingAssembly().Location;
            var fileInfo = new FileInfo(location);
            this.appsFileService.Setup(x => x.StratisAppsFolderPath).Returns(Directory.GetCurrentDirectory());
            this.appsFileService.Setup(x => x.GetStratisAppConfigFileInfos()).Returns(new List<FileInfo> { fileInfo });            
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "displayName")).Returns("app");
            this.appsFileService.Setup(x => x.GetConfigSetting(fileInfo, "webRoot")).Returns("root");

            var store = new AppsStore(this.loggerFactory, this.appsFileService.Object, this.appFactory);

            Assert.Equal(Path.GetDirectoryName(location), store.Applications.First().Location);
        }
    }   
}
