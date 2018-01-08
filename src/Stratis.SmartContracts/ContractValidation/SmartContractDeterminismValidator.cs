using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.Exceptions;
using System;
using System.Collections.Generic;

namespace Stratis.SmartContracts.ContractValidation
{
    /// <summary>
    /// TODO: Before this is ever close to being used in a test or production environment, 
    /// ensure that NO P/INVOKE OR INTEROP or other outside calls can be made.
    /// Also check there is no way around these rules, including recursion, funky namespaces,
    /// partial classes and extension methods, attributes
    /// </summary>
    internal class SmartContractDeterminismValidator : ISmartContractValidator
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

        private List<SmartContractValidationError> _errors;
        private HashSet<string> _visitedMethods;

        private MethodDefinition _currentMethod;
        private MethodDefinition _lastUserCall;

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            _errors = new List<SmartContractValidationError>();
            _visitedMethods = new HashSet<string>();

            foreach (var method in decompilation.ContractType.Methods)
            {
                _currentMethod = method;
                ValidateDeterminismForUserMethod(method);
            }

            return new SmartContractValidationResult(_errors);
        }

        public void ValidateDeterminismForUserMethod(MethodDefinition method)
        {
            // If it's a call we have already analyzed, we already know it's safe
            if (_visitedMethods.Contains(method.FullName))
                return;

            foreach (var inst in method.Body.Instructions)
            {
                if (inst.Operand is MethodReference)
                {
                    var methodReference = (MethodReference)inst.Operand;
                    var newMethodDefinition = methodReference.Resolve();
                    _lastUserCall = newMethodDefinition;
                }

                try
                {
                    ValidateInstruction(inst);
                }
                catch (StratisCompilationException e)
                {
                    _errors.Add(new SmartContractValidationError(e.Message));
                }
            }

            _visitedMethods.Add(method.FullName);
        }

        public void ValidateInstruction(Instruction inst)
        {
            if (_redLightOpCodes.Contains(inst.OpCode))
                throw new NonDeterministicCallException("Float used within " + _lastUserCall + " in " + _currentMethod);

            if (inst.Operand is FieldReference)
            {
                var fieldReference = (FieldReference)inst.Operand;
                if (_redLightFields.Contains(fieldReference.FullName))
                    throw new NonDeterministicCallException(fieldReference.FullName + " in " + _currentMethod + " is not deterministic.");
            }

            if (inst.Operand is MethodReference)
            {
                var methodReference = (MethodReference)inst.Operand;
                var newMethodDefinition = methodReference.Resolve();
                ValidateDeterminismForNonUserMethod(newMethodDefinition);
            }
        }

        public void ValidateDeterminismForNonUserMethod(MethodDefinition method)
        {
            // Safe
            if (_greenLightMethods.Contains(method.FullName) // A method we know is safe
                || _greenLightTypes.Contains(method.DeclaringType.FullName) // A type we know is safe
                || _visitedMethods.Contains(method.FullName)) // A call we have already analyzed
                return;
            
            // Non-deterministic
            if (method.IsNative || method.IsPInvokeImpl || method.IsUnmanaged || method.IsInternalCall // Instruction accesses external info.
                || _redLightTypes.Contains(method.DeclaringType.FullName)) // A type we know is dangerous
                throw new NonDeterministicCallException(_lastUserCall + " in " + _currentMethod.FullName + " is not deterministic.");

            if (method.Body?.Instructions != null)
            {
                foreach (var inst in method.Body.Instructions)
                {
                    ValidateInstruction(inst);
                }
            }

            _visitedMethods.Add(method.FullName);
        }
    }
}
