using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.SmartContracts.Exceptions;
using Stratis.SmartContracts.ContractValidation.Result;

namespace Stratis.SmartContracts.ContractValidation
{
    internal class ContractFormatValidator : IContractValidator
    {
        private readonly ModuleDefinition _moduleDefinition;
        private readonly TypeDefinition _contractType;
        private List<ContractValidationError> _errors;

        /// <summary>
        /// TODO: These should be the same assemblies used during compilation of the initial file.
        /// Should check if it can compile without these, and if not, we abandon.
        /// </summary>
        private static readonly HashSet<Assembly> _allowedAssemblies = new HashSet<Assembly>
        {
            typeof(CompiledSmartContract).Assembly,
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(List<object>).Assembly
        };

        public ContractFormatValidator(ModuleDefinition moduleDefinition,TypeDefinition typeDefinition)
        {
            _moduleDefinition = moduleDefinition;
            _contractType = typeDefinition;
            _errors = new List<ContractValidationError>();
        }

        public ContractValidationResult Validate()
        {
            ValidateLimitedAssemblyReferences();
            ValidateClassLimitations();
            return new ContractValidationResult(_errors);
            //return new ContractValidationResult();
        }

        public void ValidateLimitedAssemblyReferences()
        {
            // TODO: This check needs to be more robust -> Can we use publicKeyToken?
            foreach(var assemblyReference in _moduleDefinition.AssemblyReferences)
            {
                if (!_allowedAssemblies.Any(x => x.FullName == assemblyReference.FullName))
                    _errors.Add(new ContractValidationError("Assembly " + assemblyReference.FullName + " is not allowed."));
            }
        }


        /// <summary>
        /// For version 1, only allow a single class, which must inherit from compiledsmartcontract.
        /// </summary>
        public void ValidateClassLimitations()
        {
            var nonModuleTypes = _moduleDefinition.Types.Where(x => x.FullName != "<Module>").ToList();

            if (nonModuleTypes.Count != 1)
            {
                _errors.Add(new ContractValidationError("Only the compilation of a single class is allowed."));
                return;
            }

            var nonModuleType = nonModuleTypes.FirstOrDefault();

            if (nonModuleType.Namespace != "")
                _errors.Add(new ContractValidationError("Class must not have a namespace."));

            // TODO: This currently gets caught up on LINQ methods and actions (e.g. current 'Experiment' contract).
            if (nonModuleType.HasNestedTypes)
                _errors.Add(new ContractValidationError("Only the compilation of a single class is allowed. Includes inner types."));

            // TODO: Again, can check be more robust?
            if (typeof(CompiledSmartContract).FullName != nonModuleTypes.FirstOrDefault().BaseType.FullName)
                _errors.Add(new ContractValidationError("Contract must implement the CompiledSmartContract class."));
        }


    }
}
