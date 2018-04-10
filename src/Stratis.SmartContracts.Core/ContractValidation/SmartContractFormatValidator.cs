using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Compilation;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public class SmartContractFormatValidator : ISmartContractValidator
    {
        private List<SmartContractValidationError> errors;

        /// <summary>
        /// The referenced assemblies allowed in the smart contract
        /// </summary>
        private static readonly IEnumerable<Assembly> AllowedAssemblies = ReferencedAssemblyResolver.AllowedAssemblies;

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            this.errors = new List<SmartContractValidationError>();
            this.ValidateLimitedAssemblyReferences(decompilation.ModuleDefinition.AssemblyReferences);
            this.ValidateClassLimitations(decompilation.ModuleDefinition.Types);
            this.ValidateConstructor(decompilation.ContractType);
            return new SmartContractValidationResult(this.errors);
        }

        private void ValidateConstructor(TypeDefinition contractType)
        {
            if (contractType == null)
            {
                // Already check earlier for multiple exported types
                return;
            }

            var singleConstructorValidator = new SingleConstructorValidator();
            var constructorParamValidator = new ConstructorParamValidator();
            
            this.errors.AddRange(singleConstructorValidator.Validate(contractType));
            this.errors.AddRange(constructorParamValidator.Validate(contractType));
        }

        public void ValidateLimitedAssemblyReferences(IEnumerable<AssemblyNameReference> assemblyReferences)
        {
            foreach (AssemblyNameReference assemblyReference in assemblyReferences)
            {
                if (!AllowedAssemblies.Any(assemblyName => assemblyName.FullName == assemblyReference.FullName))
                    this.errors.Add(new SmartContractValidationError("Assembly " + assemblyReference.FullName + " is not allowed."));
            }
        }


        /// <summary>
        /// For version 1, only allow a single class, which must inherit from compiledsmartcontract.
        /// </summary>
        public void ValidateClassLimitations(IEnumerable<TypeDefinition> typeDefinitions)
        {
            typeDefinitions = typeDefinitions.Where(x => x.FullName != "<Module>").ToList();

            if (typeDefinitions.Count() != 1)
            {
                this.errors.Add(new SmartContractValidationError("Only the compilation of a single class is allowed."));
                return;
            }

            TypeDefinition nonModuleType = typeDefinitions.FirstOrDefault();

            if (nonModuleType.Namespace != "")
                this.errors.Add(new SmartContractValidationError("Class must not have a namespace."));

            // TODO: This currently gets caught up on LINQ methods and actions (e.g. current 'Experiment' contract).
            if (nonModuleType.HasNestedTypes)
                this.errors.Add(new SmartContractValidationError("Only the compilation of a single class is allowed. Includes inner types."));

            // TODO: Again, can check be more robust?
            if (typeof(SmartContract).FullName != typeDefinitions.FirstOrDefault().BaseType.FullName)
                this.errors.Add(new SmartContractValidationError("Contract must implement the SmartContract class."));
        }


    }
}
