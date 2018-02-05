using Mono.Cecil;
using Stratis.SmartContracts.Exceptions;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Stratis.SmartContracts.ContractValidation
{
    /// <summary>
    /// TODO: Before this is ever close to being used in a test or production environment, 
    /// ensure that NO P/INVOKE OR INTEROP or other outside calls can be made.
    /// Also check there is no way around these rules, including recursion, funky namespaces,
    /// partial classes and extension methods, attributes
    /// </summary>
    public class SmartContractDeterminismValidator : ISmartContractValidator
    {
        /// <summary>
        /// System calls where we don't need to check any deeper - we just allow them.
        /// Sometimes contain 'non-deterministic' calls - e.g. if Resources file was changed.
        /// We assume all resource files are the same, as set in the CompiledSmartContract constructor.
        /// </summary>
        private static readonly HashSet<string> GreenLightMethods = new HashSet<string>
        {
            "System.String System.SR::GetResourceString(System.String,System.String)"
        };

        private static readonly HashSet<string> GreenLightTypes = new HashSet<string>
        {
            "System.Boolean",
            "System.Byte",
            "System.SByte",
            "System.Char",
            "System.Int32",
            "System.UInt32",
            "System.Int64",
            "System.UInt64",
            "System.Object",
            "System.String",
            "System.Array",
            "System.Exception",
            "System.Collections.Generic.Dictionary`2",
            "System.Collections.Generic.List`1",
            "System.Linq.Enumerable",
            "Stratis.SmartContracts.SmartContractList`1",
            "Stratis.SmartContracts.SmartContractDictionary`2",
            "Stratis.SmartContracts.SmartContractMapping`2",
            typeof(PersistentState).FullName,
            typeof(CompiledSmartContract).FullName
        };

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var errors = new List<SmartContractValidationError>();

            var userDefinedMethods = decompilation.ContractType.Methods.Where(method => method.Body != null);
            var allMethods = new Dictionary<string, MethodDefinition>();

            // Build a dict of all referenced methods
            foreach (var method in userDefinedMethods)
            {
                // Exclude safe methods
                if (GreenLightMethods.Contains(method.FullName) // A method we know is safe
                    || GreenLightTypes.Contains(method.DeclaringType.FullName)) // A type we know is safe
                    continue;

                GetMethods(method, allMethods);
            }

            foreach (var method in allMethods)
            {
                errors.AddRange(ValidateDeterminismForMethod(method.Value));
            }

            return new SmartContractValidationResult(errors);
        }

        private static void GetMethods(MethodDefinition methodDefinition, IDictionary<string, MethodDefinition> visitedMethods)
        {
            if (methodDefinition.Body == null)
                return;

            var methods = methodDefinition.Body.Instructions
                .Select(instr => instr.Operand)
                .OfType<MethodReference>();
            
            foreach (var method in methods)
            {
                // Safe
                if (GreenLightMethods.Contains(method.FullName) // A method we know is safe
                    || GreenLightTypes.Contains(method.DeclaringType.FullName)) // A type we know is safe
                    continue;

                var newMethod = method.Resolve();

                if (visitedMethods.ContainsKey(newMethod.FullName))
                {
                    continue;
                }

                visitedMethods.Add(newMethod.FullName, newMethod);
                GetMethods(newMethod, visitedMethods);
            }
        }

        private static IEnumerable<SmartContractValidationError> ValidateDeterminismForMethod(MethodDefinition method)
        {
            var errors = new List<SmartContractValidationError>();

            var validators = new List<IMethodDefinitionValidator>
            {
                new MethodFlagValidator(),
                new MethodAllowedTypeValidator(),
                new GetHashCodeValidator(),
                new MethodInstructionValidator()
            };

            foreach (var validator in validators)
            {
                var validationResult = validator.Validate(method);
                errors.AddRange(validationResult);
            }

            return errors;
        }
    }
}
