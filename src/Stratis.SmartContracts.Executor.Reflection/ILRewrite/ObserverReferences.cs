using System.Reflection;
using Mono.Cecil;
using RuntimeObserver;

namespace Stratis.SmartContracts.Executor.Reflection.ILRewrite
{
    /// <summary>
    /// Direct references to the field and methods on our <see cref="Observer"/> that we need in our IL. 
    /// </summary>
    internal class ObserverReferences
    {
        /// <summary>
        /// The actual field to load onto the stack which we can call methods on. 
        /// </summary>
        public FieldDefinition InstanceField { get; }

        /// <summary>
        /// Reference to the SpendGas method on the <see cref="Observer"/>.
        /// </summary>
        public MethodReference SpendGasMethod { get; }

        /// <summary>
        /// Reference to the SpendMemory method on the <see cref="Observer"/>.
        /// </summary>
        public MethodReference SpendMemoryMethod { get; }

        public ObserverReferences(FieldDefinition instanceField, ModuleDefinition module)
        {
            this.InstanceField = instanceField;
            this.SpendGasMethod = module.ImportReference(MethodInfos.SpendGas);
            this.SpendMemoryMethod = module.ImportReference(MethodInfos.SpendMemory);
        }

        /// <summary>
        /// Used as a helper to retrieve the methods we need on <see cref="Observer"/> for the IL rewrite.
        /// </summary>
        private static class MethodInfos
        {
            public static readonly MethodInfo SpendGas = typeof(Observer).GetMethod(nameof(Observer.SpendGas));
            public static readonly MethodInfo SpendMemory = typeof(Observer).GetMethod(nameof(Observer.SpendMemory));
        }
    }
}
