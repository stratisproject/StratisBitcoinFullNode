using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.Apps.Tests.AppStore
{
    public class AppsStoreTests
    {
        private readonly IAppsStore appStore;
        private readonly FakeAppsFileService fileService = new FakeAppsFileService();

        public AppsStoreTests()
        {
            this.appStore = new Apps.AppsStore(new Mock<ILoggerFactory>().Object, this.fileService);
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
            Assert.Equal("app2", apps.Last().DisplayName);
        }
    }
}
