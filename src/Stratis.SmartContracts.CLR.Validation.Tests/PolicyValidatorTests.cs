using Stratis.SmartContracts.CLR.Validation.Policy;
using Xunit;

namespace Stratis.SmartContracts.CLR.Validation.Tests
{
    public class PolicyValidatorTests
    {
        [Fact]
        public void PolicyValidator_No_Namespace_Should_Deny_Type()
        {
            var testPolicy = new WhitelistPolicy();
            
            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object");

            Assert.Equal(PolicyValidatorResultKind.DeniedNamespace, result.Kind);
        }

        [Fact]
        public void PolicyValidator_No_Namespace_Should_Deny_Member()
        {
            var testPolicy = new WhitelistPolicy();

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object", "ToString");

            Assert.Equal(PolicyValidatorResultKind.DeniedNamespace, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Denied_Namespace_Should_Deny_Type_Without_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied);

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object");

            Assert.Equal(PolicyValidatorResultKind.DeniedType, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Denied_Namespace_Should_Deny_Member_Without_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied);

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object", "ToString");

            Assert.Equal(PolicyValidatorResultKind.DeniedType, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Allowed_Namespace_Should_Allow_Type_Without_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Allowed);

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object");
            var result2 = r.Filter("System", "Int32");

            Assert.Equal(PolicyValidatorResultKind.Allowed, result.Kind);
            Assert.Equal(PolicyValidatorResultKind.Allowed, result2.Kind);
        }

        [Fact]
        public void PolicyValidator_Allowed_Namespace_Should_Allow_Member_With_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Allowed);

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object", "ToString");
            var result2 = r.Filter("System", "Int32", "Parse");

            Assert.Equal(PolicyValidatorResultKind.Allowed, result.Kind);
            Assert.Equal(PolicyValidatorResultKind.Allowed, result2.Kind);
        }

        [Fact]
        public void PolicyValidator_Allowed_Namespace_Should_Deny_Type_With_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Allowed, t => t.Type("Object", AccessPolicy.Denied));

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object");

            Assert.Equal(PolicyValidatorResultKind.DeniedType, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Denied_Namespace_Should_Allow_Type_With_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t => t.Type("Object", AccessPolicy.Allowed));

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object");

            Assert.Equal(PolicyValidatorResultKind.Allowed, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Denied_Namespace_Allowed_Type_Should_Allow_Member_Without_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, t => t.Type("Object", AccessPolicy.Allowed));

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object", "ToString");

            Assert.Equal(PolicyValidatorResultKind.Allowed, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Allowed_Namespace_Denied_Type_Should_Deny_Member_With_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Allowed,
                    t => t.Type("Object", AccessPolicy.Denied,
                        m => m.Member("ToString", AccessPolicy.Denied)));

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object", "ToString");

            Assert.Equal(PolicyValidatorResultKind.DeniedMember, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Allowed_Namespace_Denied_Type_Should_Allow_Member_With_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Allowed,
                    t => t.Type("Object", AccessPolicy.Denied,
                        m => m.Member("ToString", AccessPolicy.Allowed)));

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object", "ToString");

            Assert.Equal(PolicyValidatorResultKind.Allowed, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Denied_Namespace_Allowed_Type_Should_Deny_Member_With_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied, 
                    t => t.Type("Object", AccessPolicy.Allowed,
                        m => m.Member("ToString", AccessPolicy.Denied)));

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object", "ToString");

            Assert.Equal(PolicyValidatorResultKind.DeniedMember, result.Kind);
        }

        [Fact]
        public void PolicyValidator_Denied_Namespace_Allowed_Type_Should_Allow_Member_With_Rule()
        {
            var testPolicy = new WhitelistPolicy()
                .Namespace("System", AccessPolicy.Denied,
                    t => t.Type("Object", AccessPolicy.Allowed,
                        m => m.Member("ToString", AccessPolicy.Allowed)));

            var r = new WhitelistPolicyFilter(testPolicy);

            var result = r.Filter("System", "Object", "ToString");

            Assert.Equal(PolicyValidatorResultKind.Allowed, result.Kind);
        }
    }
}