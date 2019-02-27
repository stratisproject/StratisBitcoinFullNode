using Stratis.SmartContracts.CLR.Validation.Policy;
using Xunit;

namespace Stratis.SmartContracts.CLR.Validation.Tests
{
    public class PolicyTests
    {
        [Fact]
        public void ValidationPolicy_Should_Overwrite_Previous_NamespacePolicy()
        {
            var policy = new WhitelistPolicy();

            policy.Namespace("Test", AccessPolicy.Allowed);

            var item = policy.Namespaces["Test"];

            policy.Namespace("Test", AccessPolicy.Allowed);

            var item2 = policy.Namespaces["Test"];

            Assert.NotEqual(item, item2);
        }
    }
}
