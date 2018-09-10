using System;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    /// <summary>
    /// Must be applied to a module after the <see cref="ObserverRewriter"/>.
    /// </summary>
    public class MemoryLimitRewriter : IILRewriter
    {
        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            FieldDefinition instance = GetObserverInstance(module);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the same Observer instance that was injected. 
        /// </summary>
        private FieldDefinition GetObserverInstance(ModuleDefinition module)
        {
            TypeDefinition injectedType = module.GetType(ObserverRewriter.InjectedNamespace, ObserverRewriter.InjectedTypeName);

            if (injectedType == null)
                throw new NotSupportedException("Can only rewrite assemblies with an Observer injected.");

            return injectedType.Fields.FirstOrDefault(x => x.Name == ObserverRewriter.InjectedPropertyName);
        }
    }
}
