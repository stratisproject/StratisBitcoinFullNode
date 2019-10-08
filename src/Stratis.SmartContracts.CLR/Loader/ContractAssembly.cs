using System;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.Loader
{
    /// <summary>
    /// Represents a smart contract assembly, which can contain multiple contract Types.
    /// </summary>
    public class ContractAssembly : IContractAssembly
    {
        public ContractAssembly(Assembly assembly)
        {
            this.Assembly = assembly;
        }

        /// <summary>
        /// The contract's underlying <see cref="System.Reflection.Assembly"/>.
        /// </summary>
        public Assembly Assembly { get; }

        /// <summary>
        /// Returns the <see cref="Type"/> with the given name on the contract assembly.
        /// </summary>
        public Type GetType(string name)
        {
            return this.Assembly.ExportedTypes.FirstOrDefault(x => x.Name == name);
        }

        private Type GetObserverType()
        {
            return this
                .Assembly
                .DefinedTypes
                .FirstOrDefault(t => t.Name == ObserverRewriter.InjectedTypeName);
        }

        /// <summary>
        /// Sets the execution context on the executing contract assembly.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool SetExecutionContext(ExecutionContext context)
        {
            Type observerType = this.GetObserverType();

            if (observerType == null)
                return false;

            // TODO get method and invoke via reflection with the context ID.

            return true;
        }
    }
}
