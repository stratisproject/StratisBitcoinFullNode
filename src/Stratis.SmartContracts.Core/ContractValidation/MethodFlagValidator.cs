using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public class NativeMethodFlagValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "Native Flag Set";

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            // Instruction accesses external info.
            var invalid = method.IsNative;

            if (invalid)
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }

    public class PInvokeImplFlagValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "PInvokeImpl Flag Set";

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            // Instruction accesses external info.
            var invalid = method.IsPInvokeImpl;

            if (invalid)
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }

    public class UnmanagedFlagValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "Unmanaged Flag Set";

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            // Instruction accesses external info.
            var invalid = method.IsUnmanaged;

            if (invalid)
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }

    public class InternalFlagValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "Internal Flag Set";

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            // Instruction accesses external info.
            var invalid = method.IsInternalCall;

            if (invalid)
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }

    public class MethodFlagValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "Invalid Method Flags";

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {            
            // Instruction accesses external info.
            var invalid = method.IsNative || method.IsPInvokeImpl || method.IsUnmanaged || method.IsInternalCall;

            if (invalid)
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}