using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    public interface IObserverMethodRewriter
    {
        /// <summary>
        /// Rewrites a method's IL to use the Observer to track some runtime metrics.
        /// </summary>
        void Rewrite(MethodDefinition methodDefinition, ILProcessor il, ObserverRewriterContext context);
    }
}
