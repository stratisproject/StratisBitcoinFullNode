using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.ModuleDefinition"/> contains a single Type
    /// </summary>
    public class SingleTypeValidator : IModuleDefinitionValidator
    {
        public IEnumerable<SmartContractValidationError> Validate(ModuleDefinition module)
        {
            List<TypeDefinition> typeDefinitions = module.Types.Where(x => x.FullName != "<Module>").ToList();

            if (typeDefinitions.Count != 1)
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError("Only the compilation of a single class is allowed.")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}