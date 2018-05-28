using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.ModuleDefinition"/> contains a single Type
    /// </summary>
    public class SingleTypeValidator : IModuleDefinitionValidator
    {
        private static readonly HashSet<string> IgnoredInCount = new HashSet<string>
        {
            "<Module>", // Part of every module
            "<PrivateImplementationDetails>" // Added when constructing an array
        };

        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            List<TypeDefinition> typeDefinitions = module.Types.Where(x => !IgnoredInCount.Contains(x.FullName)).ToList();

            if (typeDefinitions.Count != 1)
            {
                return new []
                {
                    new ModuleDefinitionValidationResult("Only the compilation of a single class is allowed.")
                };
            }

            return Enumerable.Empty<ModuleDefinitionValidationResult>();
        }
    }
}