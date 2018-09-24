using Mono.Cecil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public interface IILRewriter
    {
        /// <summary>
        /// Rewrites the IL of the given module and returns it.
        /// </summary>
        ModuleDefinition Rewrite(ModuleDefinition module);
    }
}
