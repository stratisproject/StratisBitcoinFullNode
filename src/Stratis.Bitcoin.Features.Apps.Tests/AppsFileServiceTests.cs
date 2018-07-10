using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Xunit;

namespace Stratis.Bitcoin.Features.Apps.Tests
{
    public class AppsFileServiceTests
    {
        private class StratisAppForJson
        {
            public string displayName { get; set; }
            public string webRoot { get; set; }
        }

        [Fact]
        public void Test_StratisAppsFolderPath_throws_where_directory_does_not_exist()
        {
            var dataFolder = new DataFolder(DateTime.Now.ToString(CultureInfo.CurrentCulture));            

            Assert.Throws<DirectoryNotFoundException>(() => new AppsFileService(dataFolder));
        }

        [Fact]
        public void Test_GetStratisAppConfigFileInfos_returns_stratisApp_FileInfo()
        {
            CreateStratisAppJsonAndRunTest(() =>
            {
                var service = new AppsFileService(new DataFolder(Directory.GetCurrentDirectory()));
                IEnumerable<FileInfo> fileInfos = service.GetStratisAppConfigFileInfos();
                Assert.Single(fileInfos);
            });      
        }

        [Fact]
        public void Test_GetConfigurationFields_returns_displayName()
        {
            const string displayName = "application1";
            var stratisApp = new StratisAppForJson {displayName = displayName};

            CreateStratisAppJsonAndRunTest(() =>
            {
                var dataFolder = new DataFolder(Directory.GetCurrentDirectory());
                var service = new AppsFileService(dataFolder);
                var fileInfo = new FileInfo(Path.Combine(service.StratisAppsFolderPath+"\\app", AppsFileService.StratisAppFileName));                
                Assert.Equal(displayName, service.GetConfigSetting(fileInfo, "displayName"));

            }, stratisApp);
        }

        [Fact]
        public void Test_GetConfigurationFields_returns_webRoot()
        {
            const string webRoot = "app1root";
            var stratisApp = new StratisAppForJson { webRoot = webRoot };

            CreateStratisAppJsonAndRunTest(() =>
            {
                var dataFolder = new DataFolder(Directory.GetCurrentDirectory());
                var service = new AppsFileService(dataFolder);
                var fileInfo = new FileInfo(Path.Combine(service.StratisAppsFolderPath + "\\app", AppsFileService.StratisAppFileName));
                Assert.Equal(webRoot, service.GetConfigSetting(fileInfo, "webRoot"));
                
            }, stratisApp);
        }

        private static void CreateStratisAppJsonAndRunTest(Action test, StratisAppForJson stratisApp = null)
        {
            string appDirectory = string.Empty, appJsonPath = string.Empty, currentDirectory = Directory.GetCurrentDirectory();
            StreamWriter appJsonFileStream = null;
            try
            {
                appDirectory = Path.Combine(currentDirectory, @"apps\app");
                appJsonPath = Path.Combine(appDirectory, AppsFileService.StratisAppFileName);
                Directory.CreateDirectory(appDirectory);
                appJsonFileStream = File.CreateText(appJsonPath);

                if (stratisApp != null)
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(appJsonFileStream, stratisApp);
                    appJsonFileStream.Dispose();
                    appJsonFileStream = null;
                }

                test();
            }
            finally
            {
                appJsonFileStream?.Dispose();
                if (File.Exists(appJsonPath)) File.Delete(appJsonPath);
                if (Directory.Exists(appDirectory)) Directory.Delete(appDirectory);
                var appsDirectory = Path.Combine(currentDirectory, "apps");
                if (Directory.Exists(appsDirectory)) Directory.Delete(appsDirectory);
            }
        }
    }
}
