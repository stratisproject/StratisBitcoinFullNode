using Stratis.SmartContracts.CLR.Validation.Policy;

namespace Stratis.SmartContracts.CLR.Validation
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