namespace Stratis.SmartContracts.Core.Validation.Policy
{
    public class MemberPolicy
    {        
        public MemberPolicy(string name, AccessPolicy accessPolicyPolicy)
        {
            this.Name = name;
            this.AccessPolicy = accessPolicyPolicy;
        }

        public string Name { get; }

        public AccessPolicy AccessPolicy { get; }
    }
}