using System.Linq;
using Stratis.SmartContracts.CLR.Validation.Policy;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validates a namespace/type/member for whitelisting
    /// </summary>
    public class WhitelistPolicyFilter
    {
        public WhitelistPolicyFilter(WhitelistPolicy policy)
        {
            this.Policy = policy;
        }

        public WhitelistPolicy Policy { get; }

        public PolicyValidationResult Filter(string ns, string typeName, string memberName = null)
        {
            // If there's no rule for this namespace
            if (!this.Policy.Namespaces.TryGetValue(ns, out NamespacePolicy namespaceRule))
                return new PolicyValidationResult(PolicyValidatorResultKind.DeniedNamespace);

            // If there's no rule for this Type, use the Namespace's permissivity
            if (!namespaceRule.Types.TryGetValue(typeName, out TypePolicy typeRule))
            {
                return namespaceRule.AccessPolicy == AccessPolicy.Allowed 
                    ? new PolicyValidationResult(PolicyValidatorResultKind.Allowed) 
                    : new PolicyValidationResult(PolicyValidatorResultKind.DeniedType);
            }
            
            if (typeRule.AccessPolicy == AccessPolicy.Denied && !typeRule.Members.Any())
                return new PolicyValidationResult(PolicyValidatorResultKind.DeniedType);

            if (memberName == null)
                return new PolicyValidationResult(PolicyValidatorResultKind.Allowed);

            if (!typeRule.Members.TryGetValue(memberName, out MemberPolicy memberRule))
            {
                return typeRule.AccessPolicy == AccessPolicy.Allowed 
                    ? new PolicyValidationResult(PolicyValidatorResultKind.Allowed) 
                    : new PolicyValidationResult(PolicyValidatorResultKind.DeniedMember);
            }

            if (memberRule.AccessPolicy == AccessPolicy.Denied)
                return new PolicyValidationResult(PolicyValidatorResultKind.DeniedMember, memberRule);

            return new PolicyValidationResult(PolicyValidatorResultKind.Allowed, memberRule);
        }
    }
}