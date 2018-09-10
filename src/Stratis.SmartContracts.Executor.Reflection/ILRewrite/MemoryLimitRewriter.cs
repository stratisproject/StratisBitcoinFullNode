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
            throw new System.NotImplementedException();
        }
    }
}
