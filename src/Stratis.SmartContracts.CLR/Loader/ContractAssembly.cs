using System;
using System.Collections.Generic;
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

            this.DeployedType = this.GetDeployedType();
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

        /// <summary>
        /// Returns the deployed contract type for the assembly. This value is cached. If the underlying assembly is modified,
        /// this may no longer return the correct type.
        /// </summary>
        /// <returns></returns>
        public Type DeployedType { get; }

        private Type GetDeployedType()
        {
            List<Type> contractTypes = this.Assembly.ExportedTypes.Where(IsContractType).ToList();

            return contractTypes.Count == 1
                ? contractTypes.First()
                : contractTypes.FirstOrDefault(x =>
                    x.CustomAttributes.Any(y => y.AttributeType == typeof(DeployAttribute)));
        }

        public static bool IsContractType(Type typeDefinition)
        {
            return typeDefinition.IsClass &&
                   !typeDefinition.IsAbstract &&
                   typeDefinition.BaseType != null &&
                   typeDefinition.BaseType == typeof(SmartContract);
        }

        private Type GetObserverType()
        {
            return this
                .Assembly
                .DefinedTypes
                .FirstOrDefault(t => t.Name == ObserverInstanceRewriter.InjectedTypeName);
        }

        /// <summary>
        /// Sets the static observer instance field for the executing contract assembly.
        /// Because each contract assembly is loaded in an isolated assembly load context,
        /// this instance field will be accessible to any object instances loaded in the context.
        /// </summary>
        /// <param name="observer">The observer to set.</param>
        /// <returns>A boolean representing whether or not setting the observer was successful.</returns>
        public bool SetObserver(Observer observer)
        {
            Type observerType = this.GetObserverType();

            if (observerType == null)
                return false;

            MethodInfo observerMethod = observerType.GetMethod(ObserverInstanceRewriter.MethodName);

            if (observerMethod == null)
                return false;

            // We don't expect this to throw if rewriting happened correctly.
            observerMethod.Invoke(null, new object[] { observer });

            return true;
        }

        /// <summary>
        /// Returns the assembly's <see cref="Observer"/> for the current thread, or null if it has not been set.
        /// </summary>
        /// <returns></returns>
        public Observer GetObserver()
        {
            return (Observer) this.GetObserverType()?
                .GetField(ObserverInstanceRewriter.InjectedPropertyName, BindingFlags.NonPublic | BindingFlags.Static)?
                .GetValue(null);
        }
    }
}
