using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.ModuleDefinition"/> only references allowed assemblies
    /// </summary>
    public class AssemblyReferenceValidator : IModuleDefinitionValidator
    {
        /// <summary>
        /// The referenced assemblies allowed in the smart contract
        /// </summary>
        private static readonly IEnumerable<Assembly> AllowedAssemblies = ReferencedAssemblyResolver.AllowedAssemblies;

        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            var errors = new List<ModuleDefinitionValidationResult>();

            foreach (AssemblyNameReference assemblyReference in module.AssemblyReferences)
            {
                if (!AllowedAssemblies.Any(assemblyName => assemblyName.FullName == assemblyReference.FullName))
                    errors.Add(new ModuleDefinitionValidationResult("Assembly " + assemblyReference.FullName + " is not allowed."));
            }

            return errors;
        }
    }
}