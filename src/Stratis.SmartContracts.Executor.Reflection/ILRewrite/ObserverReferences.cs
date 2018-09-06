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
            public static readonly MethodInfo OperationUp = typeof(Observer).GetMethod(nameof(Observer.OperationUp));
        }

        public ObserverReferences(FieldDefinition instanceField, ModuleDefinition module)
        {
            this.InstanceField = instanceField;
            this.OperationUpMethod = module.ImportReference(MethodInfos.OperationUp);
        }

        public FieldDefinition InstanceField { get; }
        public MethodReference OperationUpMethod { get; }
    }
}
