using Mono.Cecil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public class MethodGasInjector : IILRewriter
    {
        private readonly string typeName;
        private readonly string methodName;

        public MethodGasInjector(string typeName, MethodCall methodCall)
        {
            this.typeName = typeName;
            this.methodName = methodCall.Name;
        }

        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            return SmartContractGasInjector.AddGasCalculationToContractMethod(module, this.typeName, this.methodName);
        }
    }
}
