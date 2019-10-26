namespace Stratis.SmartContracts.CLR.Validation.Policy
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