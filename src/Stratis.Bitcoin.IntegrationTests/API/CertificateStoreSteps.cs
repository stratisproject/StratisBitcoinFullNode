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
    public partial class CertificateStoreSpecifications : BddSpecification
    {
        private string fileWithPassName;
        private string fileWithoutPassName;
        private DataFolder dataFolder;
        private NodeSettings settings;
        private DirectoryInfo directoryInfo;
        private CertificateStore certificateStore;
        private X509Certificate2 createdCertificate;
        private X509Certificate2 retrievedCertificate;

        private IPasswordReader passwordReader;

        public CertificateStoreSpecifications(ITestOutputHelper output)
            : base(output){}

        protected override void BeforeTest(){}

        protected override void AfterTest(){}

        private void a_certificate_store()
        {
            this.dataFolder = TestBase.CreateDataFolder(this, this.CurrentTest.DisplayName);
            this.settings = new NodeSettings(KnownNetworks.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-datadir=" + this.dataFolder.RootPath });
            this.directoryInfo = new DirectoryInfo(this.dataFolder.RootPath);

            this.passwordReader = Substitute.For<IPasswordReader>();
            this.passwordReader.ReadSecurePassword(Arg.Any<string>()).Returns(this.BuildSecureStringPassword());
            
            this.certificateStore = new CertificateStore(this.settings, this.passwordReader);
        }

        private void no_certificate_file()
        {
            this.dataFolder = TestBase.CreateDataFolder(this, this.CurrentTest.DisplayName);
            this.settings = new NodeSettings(KnownNetworks.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-datadir=" + this.dataFolder.RootPath });
            this.directoryInfo = new DirectoryInfo(this.dataFolder.RootPath);

            this.directoryInfo.EnumerateFiles().Should().BeEmpty();

            this.fileWithPassName = "test-with-pass.pfx";
            this.fileWithoutPassName = "test-without-pass.pfx";
            this.certificateStore.TryGet(this.fileWithPassName, out _).Should().BeFalse();
            this.certificateStore.TryGet(this.fileWithoutPassName, out _).Should().BeFalse();
        }

        private SecureString BuildSecureStringPassword()
        {
            var secureString = new SecureString();
            "password".ToList().ForEach(c => secureString.AppendChar(c));
            secureString.MakeReadOnly();
            return secureString;
        }

        private void a_certificate_file_with_password_is_created_on_disk()
        {
            using (var password = this.BuildSecureStringPassword())
            {
                this.createdCertificate = this.certificateStore.BuildSelfSignedServerCertificate(password);
                this.certificateStore.Save(this.createdCertificate, this.fileWithPassName, password);
            }
        }

        private void a_certificate_file_without_password_is_created_on_disk()
        {
            this.createdCertificate = this.certificateStore.BuildSelfSignedServerCertificate(new SecureString());
            this.certificateStore.Save(this.createdCertificate, this.fileWithoutPassName, new SecureString());
        }

        private void the_store_asks_for_a_password()
        {
            this.passwordReader.ReceivedWithAnyArgs(1).ReadSecurePassword(null);
        }

        private void the_store_does_not_ask_for_a_password()
        {
            this.passwordReader.DidNotReceiveWithAnyArgs().ReadSecurePassword(null);
        }

        private void the_store_reads_the_certificate_file_without_password()
        {
            this.retrievedCertificate = null;
            this.certificateStore.TryGet(this.fileWithoutPassName, out this.retrievedCertificate).Should().BeTrue();
        }
        
        private void the_store_reads_the_certificate_file_with_password()
        {
            this.retrievedCertificate = null;
            this.certificateStore.TryGet(this.fileWithPassName, out this.retrievedCertificate).Should().BeTrue();
        }

        private void the_certificate_from_file_should_have_the_correct_thumbprint()
        {
            this.retrievedCertificate.Should().NotBeNull();
            this.retrievedCertificate.Thumbprint.Should().Be(this.createdCertificate.Thumbprint);
        }
    }
}
