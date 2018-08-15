using System;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using NSubstitute.Extensions;

using Stratis.Bitcoin.Features.Api;
using Xunit;

namespace Stratis.Bitcoin.Api.Tests
{
    public class ProgramTest : IDisposable
    {
        private readonly X509Certificate2 certificateToUse;
        private readonly ICertificateStore certificateStore;
        private readonly IPasswordReader passwordReader;
        private readonly IWebHostBuilder webHostBuilder;
        private readonly ApiSettings apiSettings;

        private readonly SecureString certificatePassword;

        private X509Certificate2 certificateRetrieved;

        public ProgramTest()
        {
            this.apiSettings = new ApiSettings { UseHttps = true };
            this.certificateToUse = new X509Certificate2();
            this.passwordReader = Substitute.For<IPasswordReader>();
            this.certificatePassword = this.BuildSecureStringPassword();
            this.passwordReader.ReadSecurePassword().ReturnsForAnyArgs(this.certificatePassword);
            this.certificateStore = Substitute.For<ICertificateStore>();
            this.webHostBuilder = Substitute.For<IWebHostBuilder>();
        }

        public void Dispose()
        {
            this.certificatePassword?.Dispose();
        }

        private SecureString BuildSecureStringPassword()
        {
            var secureString = new SecureString();
            "password".ToList().ForEach(c => secureString.AppendChar(c));
            secureString.MakeReadOnly();
            return secureString;
        }

        [Theory]
        [InlineData("CustomCertificate")]
        [InlineData(ApiSettings.DefaultCertificateFileName)]
        public void Initialize_WhenCertificateAlreadyInStore_UsesCertificateOnHttpsWithKestrel(string certificateName)
        {
            this.apiSettings.UseHttps = true;
            this.apiSettings.HttpsCertificateFileName = certificateName;
            this.SetCertificateInStore(true);

            this.certificateRetrieved.Should().BeNull();

            Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateStore.DidNotReceiveWithAnyArgs().BuildSelfSignedServerCertificate(null);
            this.certificateStore.DidNotReceiveWithAnyArgs().Save(null, null, null);
            this.certificateRetrieved.Should().NotBeNull();
            this.certificateRetrieved.Should().Be(this.certificateToUse);
        }

        [Fact]
        public void Initialize_WhenNotUsing_Https_ShouldNotLookOrCreateCertificates()
        {
            this.apiSettings.UseHttps = false;
            this.SetCertificateInStore(true);

            Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateStore.DidNotReceiveWithAnyArgs().TryGet(null, out var certificate);
            this.certificateStore.DidNotReceiveWithAnyArgs().BuildSelfSignedServerCertificate(null);
            this.certificateStore.DidNotReceiveWithAnyArgs().Save(null, null, null);
        }

        private void SetCertificateInStore(bool isCertInStore)
        {
            this.certificateStore.TryGet(this.apiSettings.HttpsCertificateFileName, out this.certificateRetrieved)
                .Returns(isCertInStore)
                .AndDoes(_ => { this.certificateRetrieved = this.certificateToUse; });
        }
    }
}
