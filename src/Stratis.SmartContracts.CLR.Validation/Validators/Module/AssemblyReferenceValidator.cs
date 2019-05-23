using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Module
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.ModuleDefinition"/> only references allowed assemblies
    /// </summary>
    public class AssemblyReferenceValidator : IModuleDefinitionValidator
    {
        /// <summary>
        /// The referenced assemblies allowed in the smart contract
        /// </summary>
        private readonly IEnumerable<Assembly> allowedAssemblies;

        public AssemblyReferenceValidator(IEnumerable<Assembly> allowedAssemblies)
        {
            this.allowedAssemblies = allowedAssemblies;
        }

        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            var errors = new List<ModuleDefinitionValidationResult>();

            foreach (AssemblyNameReference assemblyReference in module.AssemblyReferences)
            {
                // Check name but not version.
                if (!this.allowedAssemblies.Any(assemblyName => assemblyName.GetName().Name == assemblyReference.Name))
                    errors.Add(new ModuleDefinitionValidationResult("Assembly " + assemblyReference.Name + " is not allowed."));
            }

            return errors;
        }
    }

    public class ModuleReferenceValidator : IModuleDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            if (module.HasModuleReferences)
            {
                return new[]
                {
                    new ModuleDefinitionValidationResult("Module references are not allowed")
                };
            }

            return Enumerable.Empty<ValidationResult>();
        }
    }
}