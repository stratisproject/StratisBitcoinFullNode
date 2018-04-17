using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Compilation;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Checks for non-deterministic properties inside smart contracts by validating them at the bytecode level.
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

        /// <summary>
        /// Types we deem safe and so allow all available methods.
        /// </summary>
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
            "Stratis.SmartContracts.SmartContractMapping`1",
            typeof(PersistentState).FullName,
            typeof(SmartContract).FullName
        };

        private static readonly IEnumerable<IMethodDefinitionValidator> UserDefinedMethodValidators = new List<IMethodDefinitionValidator>
        {
            new ReferencedMethodReturnTypeValidator(),
            new PInvokeImplFlagValidator(),
            new UnmanagedFlagValidator(),
            new InternalFlagValidator(),
            new NativeMethodFlagValidator(),
            new MethodAllowedTypeValidator(),
            new GetHashCodeValidator(),
            new MethodInstructionValidator(),
            new AnonymousTypeValidator(),
            new MethodParamValidator()
        };

        private static readonly IEnumerable<IMethodDefinitionValidator> NonUserMethodValidators = new List<IMethodDefinitionValidator>
        {
            new PInvokeImplFlagValidator(),
            new UnmanagedFlagValidator(),
            new InternalFlagValidator(),
            new NativeMethodFlagValidator(),
            new MethodAllowedTypeValidator(),
            new GetHashCodeValidator(),
            new MethodInstructionValidator()
        };

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            List<SmartContractValidationError> errors = new List<SmartContractValidationError>();

            Dictionary<string, List<SmartContractValidationError>> visitedMethods = new Dictionary<string, List<SmartContractValidationError>>();

            IEnumerable<MethodDefinition> userDefinedMethods = 
                decompilation
                    .ContractType
                    .Methods
                    .Where(method => method.Body != null);
         
            foreach (MethodDefinition method in userDefinedMethods)
            {
                // Validate and return all user method errors
                errors.AddRange(ValidateUserDefinedMethod(method));

                IEnumerable<MethodDefinition> userReferencedMethods = GetMethods(method);

                foreach (MethodDefinition referencedMethod in userReferencedMethods)
                {
                    List<SmartContractValidationError> referencedMethodValidationResult = ValidateNonUserMethod(referencedMethod, visitedMethods);

                    if (referencedMethodValidationResult.Any())
                    {
                        // Condense non-user method errors
                        errors.Add(new SmartContractValidationError(
                            method.Name,
                            method.FullName,
                            "Non-deterministic method reference",
                            $"Use of {referencedMethod.FullName} is not deterministic."
                        ));
                    }
                }
            }

            return new SmartContractValidationResult(errors);
        }

        private static IEnumerable<SmartContractValidationError> ValidateUserDefinedMethod(MethodDefinition method)
        {
            return ValidateWith(UserDefinedMethodValidators, method);
        }

        private static IEnumerable<MethodDefinition> GetMethods(MethodDefinition methodDefinition)
        {
            if (methodDefinition.Body == null)
                return Enumerable.Empty<MethodDefinition>();

            return methodDefinition.Body.Instructions
                .Select(instr => instr.Operand)
                .OfType<MethodReference>()
                .Where(referencedMethod =>
                    !(GreenLightMethods.Contains(methodDefinition.FullName)
                      || GreenLightTypes.Contains(methodDefinition.DeclaringType.FullName))
                )
                .Select(m => m.Resolve());
        }

        /// <summary>
        /// Recursively evaluates a non-user defined method and its references for determinism
        /// </summary>
        /// <param name="method"></param>
        /// <param name="visitedMethods"></param>
        /// <returns></returns>
        private static List<SmartContractValidationError> ValidateNonUserMethod(MethodDefinition method, Dictionary<string, List<SmartContractValidationError>> visitedMethods)
        {
            // If we've visited the method already we can use the existing validation errors
            if (visitedMethods.ContainsKey(method.FullName))
            {
                return visitedMethods[method.FullName];
            }

            // Validate all referenced methods
            IEnumerable<MethodDefinition> referencedMethods = GetMethods(method);

            List<SmartContractValidationError> validationErrors = new List<SmartContractValidationError>();

            foreach (MethodDefinition referencedMethod in referencedMethods)
            {
                List<SmartContractValidationError> methodValidationErrors = ValidateNonUserMethod(referencedMethod, visitedMethods);
                
                validationErrors.AddRange(methodValidationErrors);
            }

            // Validate this method
            IEnumerable<SmartContractValidationError> validationResults = ValidateWith(NonUserMethodValidators, method);

            validationErrors.AddRange(validationResults);

            // Some System methods recursively reference themselves (!) so this is needed
            if (visitedMethods.ContainsKey(method.FullName))
            {
                visitedMethods[method.FullName] = validationErrors;
            }                
            else
            {
                visitedMethods.Add(method.FullName, validationErrors);
            }

            if (validationErrors.Any())
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        method.Name,
                        method.FullName,
                        "Non-deterministic method reference",
                        $"Use of {method.FullName} is not deterministic."
                    )
                };
            }

            return new List<SmartContractValidationError>();
        }

        private static IEnumerable<SmartContractValidationError> ValidateWith(IEnumerable<IMethodDefinitionValidator> validators, MethodDefinition method)
        {
            var errors = new List<SmartContractValidationError>();

            foreach (IMethodDefinitionValidator validator in validators)
            {
                IEnumerable<SmartContractValidationError> validationResult = validator.Validate(method);
                errors.AddRange(validationResult);
            }

            return errors;
        }
    }
}
