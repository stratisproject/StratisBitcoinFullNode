using System.Reflection;
using Mono.Cecil;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    /// <summary>
    /// Direct references to the field and methods on our <see cref="Observer"/> that we need in our IL. 
    /// </summary>
    public class ObserverReferences
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

        /// <summary>
        /// Reference to the SpendMemoryInt32 method on <see cref="Observer.FlowThrough"/>.
        /// </summary>
        public MethodReference FlowThroughMemoryInt32Method { get; }

        public ObserverReferences(FieldDefinition instanceField, ModuleDefinition module)
        {
            this.InstanceField = instanceField;
            this.SpendGasMethod = module.ImportReference(MethodInfos.SpendGas);
            this.SpendMemoryMethod = module.ImportReference(MethodInfos.SpendMemory);
            this.FlowThroughMemoryInt32Method = module.ImportReference(MethodInfos.FlowThroughMemoryInt32);
        }

        /// <summary>
        /// Used as a helper to retrieve the methods we need on <see cref="Observer"/> for the IL rewrite.
        /// </summary>
        private static class MethodInfos
        {
            public static readonly MethodInfo SpendGas = typeof(Observer).GetMethod(nameof(Observer.SpendGas));
            public static readonly MethodInfo SpendMemory = typeof(Observer).GetMethod(nameof(Observer.SpendMemory));
            public static readonly MethodInfo FlowThroughMemoryInt32 = typeof(Observer.FlowThrough).GetMethod(nameof(Observer.FlowThrough.SpendMemoryInt32));
        }
    }
}
