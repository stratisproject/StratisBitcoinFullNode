using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public interface IObserverMethodRewriter
    {
        void Rewrite(MethodDefinition methodDefinition, ILProcessor il, ObserverRewriterContext context);
    }
}
