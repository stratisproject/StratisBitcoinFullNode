using Mono.Cecil;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    public class ConstructorGasInjector : IILRewriter
    {
        private readonly string typeName;

        public ConstructorGasInjector(string typeName)
        {
            this.typeName = typeName;
        }

        public ModuleDefinition Rewrite(ModuleDefinition module)
        {
            return SmartContractGasInjector.AddGasCalculationToConstructor(module, this.typeName);
        }
    }
}
