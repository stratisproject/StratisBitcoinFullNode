using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NBitcoin;
using Stratis.SmartContracts.CLR.Exceptions;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// A class representing a contract instance. Manages the lifecycle of a contract object and allows for constructor and method invocations.
    /// </summary>
    public class Contract : IContract
    {
        /// <summary>
        /// The default binding flags for matching the receive method. Matches public instance methods declared on the contract type only.
        /// </summary>
        private const BindingFlags DefaultReceiveLookup = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public;

        private const BindingFlags DefaultBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

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

        private MethodInfo receive;

        /// <summary>
        /// Returns the receive handler method defined on the inherited contract type. If no receive handler was defined, returns null.
        /// </summary>
        public MethodInfo ReceiveHandler {
            get
            {
                if (this.receive == null)
                {
                    this.receive = this.Type.GetMethod(MethodCall.ReceiveHandlerName, DefaultReceiveLookup);
                }

                return this.receive;
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
            if (call.IsReceiveHandlerCall)
            {
                return this.InvokeReceiveHandler();
            }

            object[] invokeParams = call.Parameters?.ToArray() ?? new object[0];

            if (invokeParams.Any(p => p == null))
            {
                // Do not support binding of null parameter values.
                return ContractInvocationResult.Failure(ContractInvocationErrorType.ParameterTypesDontMatch);
            }

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

            this.EnsureInitialized();

            return this.InvokeInternal(methodToInvoke, invokeParams);
        }

        private IContractInvocationResult InvokeReceiveHandler()
        {
            // Handles the scenario where no receive was defined, but it is attempted to be invoked anyway.
            // This could occur if a method invocation is directly made to the receive via a transaction.
            if (this.ReceiveHandler == null)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodDoesNotExist);

            this.EnsureInitialized();

            return this.InvokeInternal(this.ReceiveHandler, null);
        }

        /// <summary>
        /// Ensures the contract is initialized by setting its state fields.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!this.initialized)
                SetStateField(this.instance, this.State);

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
            catch (TargetParameterCountException parameterException)
            {
                // Parameter count incorrect
                // This should not happen
                return ContractInvocationResult.Failure(ContractInvocationErrorType.ParameterCountIncorrect);
            }
            catch (ArgumentException argumentException)
            {
                // Parameters do not match
                // This should not happen
                return ContractInvocationResult.Failure(ContractInvocationErrorType.ParameterTypesDontMatch);
            }
            catch (TargetInvocationException targetException)
            when (!(targetException.InnerException is OutOfGasException)
            && !(targetException.InnerException is MemoryConsumptionException))
            {
                // Method threw an exception that was not an OutOfGasException or a MemoryConsumptionException
                // TODO: OutofGas and MemoryConsumption exceptions should inherit from same base 'ResourceTrackingException'
                // which can be tracked here.
                return ContractInvocationResult.ExecutionFailure(ContractInvocationErrorType.MethodThrewException, targetException.InnerException);
            }
            catch (TargetInvocationException targetException) when (targetException.InnerException is OutOfGasException)
            {
                // Method threw an OutOfGasException
                return ContractInvocationResult.ExecutionFailure(ContractInvocationErrorType.OutOfGas, targetException.InnerException);
            }
            catch (TargetInvocationException targetException) when (targetException.InnerException is MemoryConsumptionException)
            {
                // Method threw a MemoryConsumptionException
                return ContractInvocationResult.ExecutionFailure(ContractInvocationErrorType.OverMemoryLimit, targetException.InnerException);
            }
            catch (Exception e)
            {
                // Other unexpected exceptions
                return ContractInvocationResult.ExecutionFailure(ContractInvocationErrorType.Exception, e);
            }
        }

        /// <summary>
        /// Uses reflection to set the state field on the contract object.
        /// </summary>
        private static void SetStateField(SmartContract smartContract, ISmartContractState contractState)
        {
            FieldInfo field = typeof(SmartContract).GetField("state", DefaultBindingFlags);

            field.SetValue(smartContract, contractState);
        }
    }
}