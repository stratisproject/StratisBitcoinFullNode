using System;
using System.Collections.Generic;

namespace Stratis.SmartContracts.CLR.Validation.Policy
{
    public class NamespacePolicy
    {
        private readonly IDictionary<string, TypePolicy> types = new Dictionary<string, TypePolicy>();

        public NamespacePolicy(string name, AccessPolicy accessPolicy)
        {
            this.Name = name;
            this.AccessPolicy = accessPolicy;
        }

        public string Name { get; }

        public AccessPolicy AccessPolicy { get; }

        public NamespacePolicy Type(Type type, AccessPolicy accessPolicy, Action<TypePolicy> setup = null)
        {
            return this.Type(type.Name, accessPolicy, setup);
        }

        public NamespacePolicy Type(string name, AccessPolicy accessPolicy, Action<TypePolicy> setup = null)
        {
            var rule = new TypePolicy(name, accessPolicy);

            this.types[name] = rule;
            
            setup?.Invoke(rule);

            return this;
        }

        public IReadOnlyDictionary<string, TypePolicy> Types => (IReadOnlyDictionary<string, TypePolicy>)this.types;
    }
}