using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    /// <summary>
    /// Resolves the methods referenced by a MethodDefinition. Excludes any whitelisted types
    /// </summary>
    public class ReferencedMethodResolver : IReferencedMethodResolver
    {
        private readonly IEnumerable<string> whitelistTypes;

        public ReferencedMethodResolver(IEnumerable<string> whitelistTypes)
        {
            this.whitelistTypes = whitelistTypes;
        }

        public IEnumerable<MethodDefinition> GetReferencedMethods(MethodDefinition methodDefinition)
        {
            if (methodDefinition.Body == null)
                return Enumerable.Empty<MethodDefinition>();

            IEnumerable<MethodDefinition> referenced = methodDefinition.Body.Instructions
                .Select(instr => instr.Operand)
                .OfType<MethodReference>()
                .Where(m => !this.whitelistTypes.Contains(m.DeclaringType.FullName))
                .Select(m => m.Resolve());

            return referenced;
        }
    }
}