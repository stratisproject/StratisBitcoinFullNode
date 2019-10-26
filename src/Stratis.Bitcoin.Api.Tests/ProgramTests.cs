using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Api.Tests
{
    public class ProgramTest
    {
        private readonly X509Certificate2 certificateToUse;
        private readonly ICertificateStore certificateStore;
        private readonly ApiSettings apiSettings;
        private readonly IWebHostBuilder webHostBuilder;

        private X509Certificate2 certificateRetrieved;

        public ProgramTest()
        {
            this.apiSettings = new ApiSettings(NodeSettings.Default(KnownNetworks.TestNet)) { UseHttps = true };
            this.certificateToUse = new X509Certificate2();
            this.certificateStore = Substitute.For<ICertificateStore>();
            this.webHostBuilder = Substitute.For<IWebHostBuilder>();
        }

        [Fact]
        public void Initialize_WhenCertificateRetrieved_UsesCertificateOnHttpsWithKestrel()
        {
            this.apiSettings.UseHttps = true;
            this.SetCertificateInStore(true);

            this.certificateRetrieved.Should().BeNull();

            Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateRetrieved.Should().NotBeNull();
            this.certificateRetrieved.Should().Be(this.certificateToUse);
            this.certificateStore.ReceivedWithAnyArgs(1).TryGet(null, out _);
        }

        [Fact]
        public void Initialize_WhenNotUsing_Https_ShouldNotLookForCertificates()
        {
            this.apiSettings.UseHttps = false;
            this.SetCertificateInStore(true);

             Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateStore.DidNotReceiveWithAnyArgs().TryGet(null, out _);
        }

        private void SetCertificateInStore(bool isCertInStore)
        {
            this.certificateStore.TryGet(this.apiSettings.HttpsCertificateFilePath, out this.certificateRetrieved)
                .Returns(isCertInStore)
                .AndDoes(_ => { this.certificateRetrieved = this.certificateToUse; });
        }
    }
}
