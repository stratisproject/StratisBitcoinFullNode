using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using Stratis.Bitcoin.Features.Api;
using Xunit;
using Xunit.Sdk;

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
            this.apiSettings = new ApiSettings { };
            this.certificateToUse = new X509Certificate2();
            this.certificateStore = Substitute.For<ICertificateStore>();
            this.webHostBuilder = Substitute.For<IWebHostBuilder>();
        }

        [Theory]
        [InlineData("CustomCertificate")]
        [InlineData(ApiSettings.DefaultCertificateSubjectName)]
        public void Initialize_WhenCertificateAlreadyInStore_UsesCertificateOnHttpsWithKestrel(string certificateName)
        {
            this.apiSettings.HttpsCertificateSubjectName = certificateName;
            this.SetCertificateInStore(true);
            
            Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateStore.DidNotReceiveWithAnyArgs().BuildSelfSignedServerCertificate(null,null);
            this.certificateStore.DidNotReceiveWithAnyArgs().Add(null);
        }

        [Fact]
        public void Initialize_WhenDefaultCertificateNotInStore_CreatesCertificate_And_AddsItToStore()
        {
            this.SetCertificateInStore(false);
            
            this.certificateStore
                .BuildSelfSignedServerCertificate(this.apiSettings.HttpsCertificateSubjectName, Arg.Any<string>())
                .Returns(this.certificateToUse);

            Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder);

            this.certificateStore.Received().Add(this.certificateToUse);
        }

        [Fact]
        public void Initialize_WhenCertificateIsNotDefault_AndNotInStore_ShouldError()
        {
            this.SetCertificateInStore(false);

            var nonDefaultCertificateName = "NOT" + ApiSettings.DefaultCertificateSubjectName;
            this.apiSettings.HttpsCertificateSubjectName = nonDefaultCertificateName;

            this.certificateStore
                .BuildSelfSignedServerCertificate(this.apiSettings.HttpsCertificateSubjectName, Arg.Any<string>())
                .Returns(this.certificateToUse);

            var initialiseAction = new Action(
                () => Program.Initialize(null, new FullNode(), this.apiSettings, this.certificateStore, this.webHostBuilder));

            initialiseAction.Should().Throw<FileNotFoundException>().And.Message.Should()
                .Contain(nonDefaultCertificateName);
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
