using System.Reflection;
using Mono.Cecil;
using RuntimeObserver;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    internal class ObserverReferences
    {
        private readonly ModuleDefinition _module;

        private static class MethodInfos
        {
            public static readonly MethodInfo SpendGas = typeof(Observer).GetMethod(nameof(Observer.SpendGas));
        }

        public ObserverReferences(FieldDefinition instanceField, ModuleDefinition module)
        {
            this.InstanceField = instanceField;
            this.SpendGasMethod = module.ImportReference(MethodInfos.SpendGas);
        }

        public FieldDefinition InstanceField { get; }
        public MethodReference SpendGasMethod { get; }
    }
}
