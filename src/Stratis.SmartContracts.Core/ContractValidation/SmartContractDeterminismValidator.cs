using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;
using Stratis.Validators.Net.Determinism;
using Stratis.Validators.Net.Format;

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
            "System.String System.SR::GetResourceString(System.String,System.String)",
            "System.Type System.Object::GetType()",
            "System.Void System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(System.Array,System.RuntimeFieldHandle)"
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
            new MethodParamValidator(),
            new NewObjectTypeValidator()
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

        public const string NonDeterministicMethodReference = "Non-deterministic method reference.";

        /// <inheritdoc/>
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var errors = new List<FormatValidationError>();
            var visited = new Dictionary<string, List<FormatValidationError>>();
            var contractType = decompilation.ModuleDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>");

            List<MethodDefinition> userMethods = contractType.Methods.Where(method => method.Body != null).ToList();
            foreach (MethodDefinition userMethod in userMethods)
            {
                ValidateUserMethod(errors, visited, userMethod);
                ValidatedReferencedMethods(errors, visited, userMethods, userMethod);
            }

            return new SmartContractValidationResult(errors);
        }

        /// <summary>
        /// Validates a user defined method in the smart contract.
        /// </summary>
        private static void ValidateUserMethod(List<FormatValidationError> errors, Dictionary<string, List<FormatValidationError>> visited, MethodDefinition userMethod)
        {
            if (!TryAddToVisited(visited, userMethod)) return;

            foreach (IMethodDefinitionValidator validator in UserDefinedMethodValidators)
            {
                IEnumerable<FormatValidationError> validatorResult = validator.Validate(userMethod);
                if (validatorResult.Any())
                {
                    errors.Add(validatorResult.First());
                    visited[userMethod.FullName] = validatorResult.ToList();
                }
            }
        }

        /// <summary>
        /// Validates all methods referenced inside of a user method.
        /// </summary>
        private void ValidatedReferencedMethods(List<FormatValidationError> errors, Dictionary<string, List<FormatValidationError>> visited, List<MethodDefinition> userMethods, MethodDefinition userMethod)
        {
            foreach (MethodDefinition referencedMethod in GetReferencedMethods(userMethod))
            {
                if (userMethods.Contains(referencedMethod))
                    continue;

                bool isValid = ValidateReferencedMethod(visited, userMethod, referencedMethod);
                if (!isValid)
                    errors.Add(NonDeterministicError(userMethod, referencedMethod));
            }
        }

        /// <summary>
        /// Recursively validates all methods referenced inside of a referenced method.
        /// </summary>
        private static bool ValidateReferencedMethod(Dictionary<string, List<FormatValidationError>> visited, MethodDefinition userMethod, MethodDefinition referencedMethod)
        {
            if (TryAddToVisited(visited, referencedMethod))
            {
                foreach (MethodDefinition internalMethod in GetReferencedMethods(referencedMethod))
                {
                    bool isValid = ValidateReferencedMethod(visited, referencedMethod, internalMethod);
                    if (!isValid)
                        return isValid;
                }

                foreach (IMethodDefinitionValidator validator in NonUserMethodValidators)
                {
                    List<FormatValidationError> result = validator.Validate(referencedMethod).ToList();
                    if (result.Any())
                    {
                        visited[referencedMethod.FullName] = result;
                        return false;
                    }
                }

                return true;
            }
            else
                return true;
        }

        /// <summary>
        /// Methods that are called within a given method body.
        /// <para>
        /// This returns all methods, not just the ones that are user defined.
        /// </para>
        /// </summary>
        private static IEnumerable<MethodDefinition> GetReferencedMethods(MethodDefinition methodDefinition)
        {
            if (methodDefinition.Body == null)
                return Enumerable.Empty<MethodDefinition>();

            IEnumerable<MethodDefinition> referenced = methodDefinition.Body.Instructions
                .Select(instr => instr.Operand)
                .OfType<MethodReference>()
                .Where(referencedMethod => !(GreenLightMethods.Contains(referencedMethod.FullName) 
                                             || GreenLightTypes.Contains(referencedMethod.DeclaringType.FullName)))
                .Select(m => m.Resolve());

            return referenced;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="visited"></param>
        /// <param name="method"></param>
        private static bool TryAddToVisited(Dictionary<string, List<FormatValidationError>> visited, MethodDefinition method)
        {
            if (visited.ContainsKey(method.FullName))
                return false;
            else
            {
                visited.Add(method.FullName, new List<FormatValidationError>());
                return true;
            }
        }

        /// <summary>
        /// Returns an error when a referenced method is non-deterministic in a containing method.
        /// <para>I.e. if in method A, method B is referenced and it is non-deterministic, use this method.</para>
        /// </summary>
        /// <param name="userMethod">The containing method.</param>
        /// <param name="referencedMethod">The method that is non-deterministic in the containing method.</param>
        public static FormatValidationError NonDeterministicError(MethodDefinition userMethod, MethodDefinition referencedMethod)
        {
            return new FormatValidationError(userMethod, NonDeterministicMethodReference, $"{referencedMethod.FullName} is non-deterministic.");
        }
    }
}