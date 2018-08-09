using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using Stratis.Bitcoin.Features.Api;
using Xunit;

namespace Stratis.Bitcoin.Api.Tests
{
    public class ProgramTest
    {
        private readonly X509Certificate2 certificateToUse;
        private readonly ICertificateStore certificateStore;
        private readonly IWebHostBuilder webHostBuilder;
        private readonly ApiSettings apiSettings;

        public ProgramTest()
        {
            this.apiSettings = new ApiSettings { HttpsCertificateSubjectName = ApiSettings.DefaultCertificateSubjectName};
            this.certificateToUse = new X509Certificate2();
            this.certificateStore = Substitute.For<ICertificateStore>();
            this.webHostBuilder = Substitute.For<IWebHostBuilder>();
        }

        [Fact]
        public void Initialize_WhenCertificateAlreadyInStore_UsesCertificateOnHttpsWithKestrel()
        {
            this.SetCertificateInStore(true);
            
            Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateStore.DidNotReceiveWithAnyArgs().BuildSelfSignedServerCertificate(null,null);
            this.certificateStore.DidNotReceiveWithAnyArgs().Add(null);
        }

        [Fact]
        public void Initialize_WhenCertificateNotInStore_CreatesCertificate_And_AddsItToStore()
        {
            this.SetCertificateInStore(false);


            this.certificateStore
                .BuildSelfSignedServerCertificate(this.apiSettings.HttpsCertificateSubjectName, Arg.Any<string>())
                .Returns(this.certificateToUse);

            Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateStore.Received().Add(this.certificateToUse);
        }

        [Fact]
        public void Initialize_WhenCertificateInApiSettings_ButNotInStore_ShouldError()
        {
            this.SetCertificateInStore(false);

            this.apiSettings.HttpsCertificateSubjectName = "NOT" + ApiSettings.DefaultCertificateSubjectName;

            this.certificateStore
                .BuildSelfSignedServerCertificate(this.apiSettings.HttpsCertificateSubjectName, Arg.Any<string>())
                .Returns(this.certificateToUse);

            Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateStore.DidNotReceive().Add(this.certificateToUse);
        }

        private void SetCertificateInStore(bool isCertInStore)
        {
            this.certificateStore.TryGet(this.apiSettings.HttpsCertificateSubjectName, out X509Certificate2 certificate)
                .Returns(isCertInStore)
                .AndDoes(_ => { certificate = this.certificateToUse; });
        }
    }
}
