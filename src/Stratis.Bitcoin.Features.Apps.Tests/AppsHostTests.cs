using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.Apps.Tests
{
    public class AppsHostTests : IDisposable
    {
        private readonly IAppsHost appsHost;

        public AppsHostTests()
        {
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(x => x.CreateLogger(typeof(AppsHost).FullName)).Returns(new Mock<ILogger>().Object);
            this.appsHost = new AppsHost(loggerFactory.Object);
        }

        [Fact]
        public void Test_Host_hosts_an_application()
        {            
            IStratisApp[] apps = { new StratisApp { Location = Directory.GetCurrentDirectory() } };

            this.appsHost.Host(apps);

            Assert.Single(this.appsHost.HostedApps);
        }

        [Fact]
        public void Test_Host_hosts_applications()
        {            
            IStratisApp[] apps =
            {
                new StratisApp { Location = Directory.GetCurrentDirectory() },
                new StratisApp { Location = Directory.GetCurrentDirectory() }
            };

            this.appsHost.Host(apps);

            Assert.Equal(2, this.appsHost.HostedApps.Count());
        }

        [Fact]
        public void Test_Host_hosts_only_single_page_apps()
        {
            IStratisApp[] apps =
            {
                new StratisApp { Location = Directory.GetCurrentDirectory(), IsSinglePageApp = false },
                new StratisApp { Location = Directory.GetCurrentDirectory(), IsSinglePageApp = false }
            };

            this.appsHost.Host(apps);            

            Assert.False(this.appsHost.HostedApps.Any());
        }

        [Fact]
        public void Test_Close_clears_HostedApps()
        {
            IStratisApp[] apps = { new StratisApp { Location = Directory.GetCurrentDirectory() } };

            this.appsHost.Host(apps);
            this.appsHost.Close();

            Assert.False(this.appsHost.HostedApps.Any());
        }

        [Fact]
        public void Test_apps_are_hosted_at_localhost()
        {
            IStratisApp[] apps =
            {
                new StratisApp { Location = Directory.GetCurrentDirectory() },
                new StratisApp { Location = Directory.GetCurrentDirectory() }
            };

            this.appsHost.Host(apps);

            Assert.True(this.appsHost.HostedApps.All(x => x.Address.StartsWith("http://localhost:")));
        }

        public void Dispose()
        {
            this.appsHost.Close();
        }
    }
}
