using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Stratis.SmartContracts.ContractValidation
{
    internal class GetHashCodeValidator : IMethodDefinitionValidator
    {
        public static readonly HashSet<string> RedLightMethods = new HashSet<string>
        {
            "System.Int32 System.Object::GetHashCode()"
        };

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            var errors = new List<SmartContractValidationError>();

            if (RedLightMethods.Contains(method.FullName))
            {
                errors.Add(new SmartContractValidationError($"Use of {method.FullName} is not deterministic [known non-deterministic method call]"));
            }

            return errors;
        }
    }

    internal class MethodFlagValidator : IMethodDefinitionValidator
    {
        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            var errors = new List<SmartContractValidationError>();

            // Instruction accesses external info.
            var invalid = method.IsNative || method.IsPInvokeImpl || method.IsUnmanaged || method.IsInternalCall;

            if (invalid)
            {
                errors.Add(new SmartContractValidationError($"Use of {method.FullName} is non-deterministic [invalid method flags]"));
            }

            return errors;
        }
    }

    internal class MethodTypeValidator : IMethodDefinitionValidator
    {
        private static readonly HashSet<string> _redLightTypes = new HashSet<string>
        {
            "System.Threading",
            "System.AppDomain"
        };

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            var errors = new List<SmartContractValidationError>();

            if (_redLightTypes.Contains(method.DeclaringType.FullName))
            {
                errors.Add(new SmartContractValidationError($"Use of {method.DeclaringType.FullName} is non-deterministic [known non-deterministic method call]"));
            }

            return errors;
        }
    }

    public class SmartContractMethodValidator : IMethodDefinitionValidator
    {
        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            //@todo - Validate instructions in here too
            throw new NotImplementedException();
        }
    }

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
        private static readonly HashSet<string> _greenLightMethods = new HashSet<string>
        {
            "System.String System.SR::GetResourceString(System.String,System.String)"
        };

        private static readonly HashSet<string> _greenLightTypes = new HashSet<string>
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

        private static readonly HashSet<string> _redLightTypes = new HashSet<string>
        {
            "System.Threading",
            "System.AppDomain"
        };

        /// <summary>
        /// There may be other intrinsics (values that are inserted via the compiler and are different per machine).
        /// </summary>
        private static readonly HashSet<string> _redLightFields = new HashSet<string>
        {
            "System.Boolean System.BitConverter::IsLittleEndian"
        };

        /// <summary>
        /// Any float-based instructions. Not allowed.
        /// </summary>
        private static readonly HashSet<OpCode> _redLightOpCodes = new HashSet<OpCode>
        {
            OpCodes.Ldc_R4,
            OpCodes.Ldc_R8,
            OpCodes.Ldelem_R4,
            OpCodes.Ldelem_R8,
            OpCodes.Conv_R_Un,
            OpCodes.Conv_R4,
            OpCodes.Conv_R8,
            OpCodes.Ldind_R4,
            OpCodes.Ldind_R8,
            OpCodes.Stelem_R4,
            OpCodes.Stelem_R8,
            OpCodes.Stind_R4,
            OpCodes.Stind_R8
        };

        private HashSet<string> _visitedMethods;

        private MethodDefinition _currentMethod;
        private MethodDefinition _lastUserCall;

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var errors = new List<SmartContractValidationError>();
            _visitedMethods = new HashSet<string>();

            foreach (var method in decompilation.ContractType.Methods)
            {
                _currentMethod = method;
                errors.AddRange(ValidateDeterminismForUserMethod(method));
            }

            return new SmartContractValidationResult(errors);
        }

        public IEnumerable<SmartContractValidationError> ValidateDeterminismForUserMethod(MethodDefinition method)
        {
            // If it's a call we have already analyzed, we already know it's safe
            if (_visitedMethods.Contains(method.FullName))
                return Enumerable.Empty<SmartContractValidationError>();

            var errors = new List<SmartContractValidationError>();

            foreach (var instruction in method.Body.Instructions)
            {
                var methodReference = instruction.Operand as MethodReference;

                if (methodReference != null)
                {
                    var newMethodDefinition = methodReference.Resolve();
                    _lastUserCall = newMethodDefinition;
                }

                errors.AddRange(ValidateInstruction(instruction));
            }

            _visitedMethods.Add(method.FullName);

            return errors;
        }

        public IEnumerable<SmartContractValidationError> ValidateInstruction(Instruction inst)
        {
            if (_redLightOpCodes.Contains(inst.OpCode))
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError("Float used within " + _lastUserCall + " in " + _currentMethod)
                };
            }

            if (inst.Operand is FieldReference)
            {
                var fieldReference = (FieldReference)inst.Operand;
                if (_redLightFields.Contains(fieldReference.FullName))
                {
                    return new List<SmartContractValidationError>
                    {
                        new SmartContractValidationError(fieldReference.FullName + " in " + _currentMethod + " is not deterministic.")
                    };
                }
            }

            var errors = new List<SmartContractValidationError>();

            var methodReference = inst.Operand as MethodReference;

            if (methodReference != null)
            {                
                var newMethodDefinition = methodReference.Resolve();
                var validationResults = ValidateDeterminismForNonUserMethod(newMethodDefinition);
                errors.AddRange(validationResults);
            }

            return errors;
        }

        public IEnumerable<SmartContractValidationError> ValidateDeterminismForNonUserMethod(MethodDefinition method)
        {
            if (_visitedMethods.Contains(method.FullName)) // A call we have already analyzed
            {
                return Enumerable.Empty<SmartContractValidationError>();
            }
            // Safe
            if (_greenLightMethods.Contains(method.FullName) // A method we know is safe
                || _greenLightTypes.Contains(method.DeclaringType.FullName)) // A type we know is safe
                return Enumerable.Empty<SmartContractValidationError>();

            var errors = new List<SmartContractValidationError>();

            var validators = new List<IMethodDefinitionValidator>
            {
                new MethodFlagValidator(),
                new MethodTypeValidator()
            };

            foreach (var validator in validators)
            {
                var validationResult = validator.Validate(method);
                errors.AddRange(validationResult);
            }
            
            if (method.Body?.Instructions != null)
            {
                foreach (var inst in method.Body.Instructions)
                {
                    var instructionValidationResult = ValidateInstruction(inst);
                    errors.AddRange(instructionValidationResult);
                }
            }

            _visitedMethods.Add(method.FullName);

            return errors;
        }
    }
}
