using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class CertificateStoreSpecifications : BddSpecification
    {
        [Fact]
        public void CertificateStoreCanReadAndWriteCertFiles_WithPassword_ByAskingPassword()
        {
            Given(a_certificate_store);
            And(no_certificate_file);
            When(a_certificate_file_with_password_is_created_on_disk);
            And(the_store_reads_the_certificate_file_with_password);
            Then(the_store_asks_for_a_password);
            And(the_certificate_from_file_should_have_the_correct_thumbprint);
        }

        [Fact]
        public void CertificateStoreCanReadAndWriteCertFiles_WithoutPassword_WithouthAskingPassword()
        {
            Given(a_certificate_store);
            And(no_certificate_file);
            When(a_certificate_file_without_password_is_created_on_disk);
            And(the_store_reads_the_certificate_file_without_password);
            Then(the_store_does_not_ask_for_a_password);
            And(the_certificate_from_file_should_have_the_correct_thumbprint);
        }
    }
}
