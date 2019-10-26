using System.Collections.Generic;

namespace Stratis.SmartContracts.CLR.Validation.Policy
{
    public class TypePolicy
    {
        private readonly IDictionary<string, MemberPolicy> members = new Dictionary<string, MemberPolicy>();

        public TypePolicy(string name, AccessPolicy accessPolicy)
        {
            this.Name = name;
            this.AccessPolicy = accessPolicy;
        }

        public string Name { get; }

        public AccessPolicy AccessPolicy { get; }

        public TypePolicy Member(string name, AccessPolicy accessPolicy)
        {
            this.members[name] = new MemberPolicy(name, accessPolicy);

            return this;
        }

        public TypePolicy Constructor(AccessPolicy accessPolicy)
        {
            return this.Member(".ctor", accessPolicy);
        }

        public IReadOnlyDictionary<string, MemberPolicy> Members => (IReadOnlyDictionary<string, MemberPolicy>)this.members;
    }
}