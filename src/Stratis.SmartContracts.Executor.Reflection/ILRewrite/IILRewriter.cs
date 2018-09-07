using Mono.Cecil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public interface IILRewriter
    {
        ModuleDefinition Rewrite(ModuleDefinition module);
    }
}
