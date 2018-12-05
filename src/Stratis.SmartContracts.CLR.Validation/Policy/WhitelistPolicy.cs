using System;
using System.Collections.Generic;

namespace Stratis.SmartContracts.CLR.Validation.Policy
{
    /// <summary>
    /// Defines a policy used to validate a Type hierarchy for allowed/denied Types and methods
    /// </summary>
    public class WhitelistPolicy
    {
        private readonly IDictionary<string, NamespacePolicy> namespaces = new Dictionary<string, NamespacePolicy>();

        public WhitelistPolicy Namespace(string name, AccessPolicy accessPolicy, Action<NamespacePolicy> setup = null)
        {
            var rule = new NamespacePolicy(name, accessPolicy);

            this.namespaces[name] = rule;

            setup?.Invoke(rule);

            return this;
        }

        public IReadOnlyDictionary<string, NamespacePolicy> Namespaces => (IReadOnlyDictionary<string, NamespacePolicy>) this.namespaces;      
    }
}