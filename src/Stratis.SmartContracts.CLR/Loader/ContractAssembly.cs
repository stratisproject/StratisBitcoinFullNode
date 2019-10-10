using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

        public Type GetDeployedType()
        {
            Type deployAttributeType =
                this.Assembly.ExportedTypes.FirstOrDefault(t => t.GetCustomAttribute<DeployAttribute>() != null);
            return deployAttributeType != null 
                ? deployAttributeType
                : this.Assembly.ExportedTypes.FirstOrDefault();
        }

        /// <summary>
        /// Gets the public methods defined by the contract, ignoring property getters/setters.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MethodInfo> GetPublicMethods()
        {
            Type deployedType = this.GetDeployedType();

            if (deployedType == null)
                return new List<MethodInfo>();

            return deployedType
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance) // Get only the methods declared on the contract type
                .Where(m => !m.IsSpecialName); // Ignore property setters/getters
        }

        public IEnumerable<PropertyInfo> GetPublicGetterProperties()
        {
            Type deployedType = this.GetDeployedType();

            if (deployedType == null)
                return new List<PropertyInfo>();

            return deployedType
                .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetGetMethod() != null);
        }
    }
}
