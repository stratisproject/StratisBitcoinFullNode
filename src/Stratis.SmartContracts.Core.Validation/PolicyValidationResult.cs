using Stratis.SmartContracts.Core.Validation.Policy;

namespace Stratis.SmartContracts.Core.Validation
{
    public struct PolicyValidationResult
    {
        public PolicyValidationResult(PolicyValidatorResultKind kind, MemberPolicy memberRule = null)
        {
            this.Kind = kind;
            this.MemberRule = memberRule;
        }

        public PolicyValidatorResultKind Kind { get; }
        public MemberPolicy MemberRule { get; }
    }

    public enum PolicyValidatorResultKind
    {
        DeniedNamespace,
        DeniedType,
        DeniedMember,
        Allowed
    }

}