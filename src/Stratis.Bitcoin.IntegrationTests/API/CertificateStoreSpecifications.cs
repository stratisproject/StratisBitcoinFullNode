using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NBitcoin.Protocol;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public class CertificateStoreSpecifications : BddSpecification
    {
        private string fileName;
        private DataFolder dataFolder;
        private NodeSettings settings;
        private DirectoryInfo directoryInfo;
        private CertificateStore certificateStore;
        private X509Certificate2 createdCertificate;
        private X509Certificate2 retrievedCertificate;

        public CertificateStoreSpecifications(ITestOutputHelper output)
            : base(output){}

        protected override void BeforeTest(){}

        protected override void AfterTest(){}

        [Fact]
        public void CertificateStoreCanReadAndWriteCertFiles()
        {
            Given(a_certificate_store);
            And(no_certificate_file);
            When(a_certificate_file_is_created_on_disk);
            Then(the_certificate_file_should_be_readable);
            And(the_certificate_from_file_should_have_the_correct_thumbprint);
        }

        private void a_certificate_store()
        {
            this.dataFolder = TestBase.CreateDataFolder(this, this.CurrentTest.DisplayName);
            this.settings = new NodeSettings(KnownNetworks.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-datadir=" + this.dataFolder.RootPath });
            this.directoryInfo = new DirectoryInfo(this.dataFolder.RootPath);

            var passwordReader = Substitute.For<IPasswordReader>();
            passwordReader.ReadSecurePassword(Arg.Any<string>()).Returns(this.BuildSecureStringPassword());

            this.certificateStore = new CertificateStore(this.settings, passwordReader);
        }

        private void no_certificate_file()
        {
            this.dataFolder = TestBase.CreateDataFolder(this, this.CurrentTest.DisplayName);
            this.settings = new NodeSettings(KnownNetworks.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-datadir=" + this.dataFolder.RootPath });
            this.directoryInfo = new DirectoryInfo(this.dataFolder.RootPath);

            this.directoryInfo.EnumerateFiles().Should().BeEmpty();

            this.fileName = "test.pfx";
            this.certificateStore.TryGet(this.fileName, out _).Should().BeFalse();
        }

        private SecureString BuildSecureStringPassword()
        {
            var secureString = new SecureString();
            "password".ToList().ForEach(c => secureString.AppendChar(c));
            secureString.MakeReadOnly();
            return secureString;
        }

        private void a_certificate_file_is_created_on_disk()
        {
            using (var password = this.BuildSecureStringPassword())
            {
                this.createdCertificate = this.certificateStore.BuildSelfSignedServerCertificate(password);
                this.certificateStore.Save(this.createdCertificate, this.fileName, password);
            }
        }

        private void the_certificate_file_should_be_readable()
        {
            this.retrievedCertificate = null;
            this.certificateStore.TryGet(this.fileName, out this.retrievedCertificate).Should().BeTrue();
        }

        private void the_certificate_from_file_should_have_the_correct_thumbprint()
        {
            this.retrievedCertificate.Should().NotBeNull();
            this.retrievedCertificate.Thumbprint.Should().Be(this.createdCertificate.Thumbprint);
        }
    }
}
