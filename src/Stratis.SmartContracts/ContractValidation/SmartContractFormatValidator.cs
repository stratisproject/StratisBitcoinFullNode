using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stratis.SmartContracts.ContractValidation
{
    public class SmartContractFormatValidator : ISmartContractValidator
    {
        private List<SmartContractValidationError> errors;

        /// <summary>
        /// TODO: These should be the same assemblies used during compilation of the initial file.
        /// Should check if it can compile without these, and if not, we abandon.
        /// </summary>
        private static readonly HashSet<Assembly> AllowedAssemblies = new HashSet<Assembly>
        {
            typeof(CompiledSmartContract).Assembly,
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(List<object>).Assembly
        };

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            this.errors = new List<SmartContractValidationError>();
            ValidateLimitedAssemblyReferences(decompilation.ModuleDefinition);
            ValidateClassLimitations(decompilation.ModuleDefinition);
            return new SmartContractValidationResult(this.errors);
        }

        public void ValidateLimitedAssemblyReferences(ModuleDefinition moduleDefinition)
        {
            // TODO: This check needs to be more robust -> What other way can we use?
            foreach(AssemblyNameReference assemblyReference in moduleDefinition.AssemblyReferences)
            {
                if (!AllowedAssemblies.Any(x => x.FullName == assemblyReference.FullName))
                    this.errors.Add(new SmartContractValidationError("Assembly " + assemblyReference.FullName + " is not allowed."));
            }
        }


        /// <summary>
        /// For version 1, only allow a single class, which must inherit from compiledsmartcontract.
        /// </summary>
        public void ValidateClassLimitations(ModuleDefinition moduleDefinition)
        {
            var nonModuleTypes = moduleDefinition.Types.Where(x => x.FullName != "<Module>").ToList();

            if (nonModuleTypes.Count != 1)
            {
                this.errors.Add(new SmartContractValidationError("Only the compilation of a single class is allowed."));
                return;
            }

            TypeDefinition nonModuleType = nonModuleTypes.FirstOrDefault();

            if (nonModuleType.Namespace != "")
                this.errors.Add(new SmartContractValidationError("Class must not have a namespace."));

            // TODO: This currently gets caught up on LINQ methods and actions (e.g. current 'Experiment' contract).
            if (nonModuleType.HasNestedTypes)
                this.errors.Add(new SmartContractValidationError("Only the compilation of a single class is allowed. Includes inner types."));

            // TODO: Again, can check be more robust?
            if (typeof(CompiledSmartContract).FullName != nonModuleTypes.FirstOrDefault().BaseType.FullName)
                this.errors.Add(new SmartContractValidationError("Contract must implement the CompiledSmartContract class."));
        }


    }
}
