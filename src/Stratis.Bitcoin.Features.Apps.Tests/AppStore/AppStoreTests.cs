using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.Apps.Tests.AppStore
{
    public class AppStoreTests
    {
        private readonly IAppStore appStore;
        private readonly FakeAppsFileService fileService = new FakeAppsFileService();

        public AppStoreTests()
        {
            this.appStore = new Apps.AppStore(new DataFolder("x"), new Mock<ILoggerFactory>().Object, this.fileService);
        }

        [Fact]
        public void Test_GetApplications_returns_two_fake_Stratis_apps()
        {
            var apps = new List<IStratisApp>();
            this.appStore.GetApplications().Subscribe(x => apps.AddRange(x));
            Assert.Equal(2, apps.Count);
        }

        [Fact]
        public void Test_GetApplications_first_fake_app_is_app1()
        {
            var apps = new List<IStratisApp>();
            this.appStore.GetApplications().Subscribe(x => apps.AddRange(x));
            Assert.Equal("app1", apps.First().DisplayName);
        }

        [Fact]
        public void Test_GetApplications_second_fake_app_is_app2()
        {
            var apps = new List<IStratisApp>();
            this.appStore.GetApplications().Subscribe(x => apps.AddRange(x));
            Assert.Equal("app2", apps[1].DisplayName);
        }
    }
}
