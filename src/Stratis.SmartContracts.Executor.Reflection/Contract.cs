using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NBitcoin;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// A class representing a contract instance. Manages the lifecycle of a contract object and allows for constructor and method invocations.
    /// </summary>
    public class Contract : IContract
    {
        /// <summary>
        /// The default binding flags for matching the fallback method. Matches public instance methods declared on the contract type only.
        /// </summary>
        private const BindingFlags DefaultFallbackLookup = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public;

        private readonly SmartContract instance;

        /// <summary>
        /// Indicates whether the ISmartContractState fields have been set on the instance object yet.
        /// </summary>
        private bool initialized;

        /// <inheritdoc />
        public uint160 Address { get; }

        /// <inheritdoc />
        public Type Type { get; }

        /// <inheritdoc />
        public ISmartContractState State { get; }

        private MethodInfo fallback;

        /// <summary>
        /// Returns the fallback method defined on the inherited contract type. If no fallback was defined, returns null.
        /// </summary>
        public MethodInfo Fallback {
            get
            {
                if (this.fallback == null)
                {
                    this.fallback = this.Type.GetMethod(MethodCall.FallbackMethodName, DefaultFallbackLookup);
                }

                return this.fallback;
            }
        }

        private Contract(SmartContract instance, Type type, ISmartContractState state, uint160 address)
        {
            this.instance = instance;
            this.State = state;
            this.Type = type;
            this.Address = address;
        }

        /// <summary>
        /// Creates an <see cref="IContract"/> that represents a smart contract in an uninitialized state.
        /// </summary>
        public static IContract CreateUninitialized(Type type, ISmartContractState state, uint160 address)
        {
            var contract = (SmartContract)FormatterServices.GetSafeUninitializedObject(type);

            return new Contract(contract, type, state, address);
        }

        /// <summary>
        /// Checks whether a the constructor with the given signature exists on the supplied contract Type.
        /// </summary>
        public static bool ConstructorExists(Type type, IReadOnlyList<object> parameters)
        {
            Type[] types = {typeof(ISmartContractState)};    
            
            if (parameters != null)
                types = types.Concat(parameters.Select(p => p.GetType())).ToArray();

            return type.GetConstructor(types) != null;
        }

        /// <inheritdoc />
        public IContractInvocationResult InvokeConstructor(IReadOnlyList<object> parameters)
        {
            // If it's a constructor we need to append the ISmartContractState to the start of the parameters array
            object[] invokeParams = { this.State };
            
            if(parameters != null)
                invokeParams = invokeParams.Concat(parameters).ToArray();

            Type[] types = invokeParams.Select(p => p.GetType()).ToArray();

            ConstructorInfo methodToInvoke = this.Type.GetConstructor(types);

            if (methodToInvoke == null)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodDoesNotExist);

            IContractInvocationResult result = this.InvokeInternal(methodToInvoke, invokeParams);

            this.initialized = result.IsSuccess;

            return result;
        }

        /// <inheritdoc />
        public IContractInvocationResult Invoke(MethodCall call)
        {
            if (call.IsFallbackCall)
            {
                return this.InvokeFallback();
            }

            object[] invokeParams = call.Parameters?.ToArray() ?? new object[0];

            Type[] types = invokeParams.Select(p => p.GetType()).ToArray();

            MethodInfo methodToInvoke = this.Type.GetMethod(call.Name, types);

            if (methodToInvoke == null)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodDoesNotExist);

            // This should not happen without setting the appropriate binding flags
            if (methodToInvoke.IsConstructor)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodIsConstructor);

            // This should not happen without setting the appropriate binding flags
            if (methodToInvoke.IsPrivate)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodIsPrivate);

            EnsureInitialized();

            return this.InvokeInternal(methodToInvoke, invokeParams);
        }

        private IContractInvocationResult InvokeFallback()
        {
            // Handles the scenario where no fallback was defined, but it is attempted to be invoked anyway.
            // This could occur if a method invocation is directly made to the fallback via a transaction.
            if (this.Fallback == null)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodDoesNotExist);

            EnsureInitialized();

            return this.InvokeInternal(this.Fallback, null);
        }

        /// <summary>
        /// Ensures the contract is initialized by setting its state fields.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!this.initialized)
                SetStateFields(this.instance, this.State);

            this.initialized = true;
        }

        /// <summary>
        /// Shared internal logic for invoking a method.
        /// </summary>
        private IContractInvocationResult InvokeInternal(MethodBase method, object[] parameters)
        {
            try
            {
                object result = method.Invoke(this.instance, parameters);

                return ContractInvocationResult.Success(result);
            }
            catch (ArgumentException argumentException)
            {
                // Parameters do not match
                // This should not happen
                return ContractInvocationResult.Failure(ContractInvocationErrorType.ParameterTypesDontMatch, argumentException);
            }
            catch (TargetInvocationException targetException) when (!(targetException.InnerException is OutOfGasException))
            {
                // Method threw an exception that was not an OutOfGasException
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodThrewException, targetException);
            }
            catch (TargetInvocationException targetException) when (targetException.InnerException is OutOfGasException)
            {
                // Method threw an exception that was an OutOfGasException
                return ContractInvocationResult.Failure(ContractInvocationErrorType.OutOfGas);
            }
            catch (TargetParameterCountException parameterException)
            {
                // Parameter count incorrect
                // This should not happen
                return ContractInvocationResult.Failure(ContractInvocationErrorType.ParameterCountIncorrect, parameterException);
            }            
        }

        /// <summary>
        /// Uses reflection to set the state fields on the contract object.
        /// </summary>
        private static void SetStateFields(SmartContract smartContract, ISmartContractState contractState)
        {
            FieldInfo[] fields = typeof(SmartContract).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (FieldInfo field in fields)
            {
                switch (field.Name)
                {
                    case "gasMeter":
                        field.SetValue(smartContract, contractState.GasMeter);
                        break;
                    case "Block":
                        field.SetValue(smartContract, contractState.Block);
                        break;
                    case "getBalance":
                        field.SetValue(smartContract, contractState.GetBalance);
                        break;
                    case "internalTransactionExecutor":
                        field.SetValue(smartContract, contractState.InternalTransactionExecutor);
                        break;
                    case "internalHashHelper":
                        field.SetValue(smartContract, contractState.InternalHashHelper);
                        break;
                    case "Message":
                        field.SetValue(smartContract, contractState.Message);
                        break;
                    case "PersistentState":
                        field.SetValue(smartContract, contractState.PersistentState);
                        break;
                    case "smartContractState":
                        field.SetValue(smartContract, contractState);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}