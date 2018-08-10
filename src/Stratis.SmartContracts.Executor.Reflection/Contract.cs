using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NBitcoin;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class Contract : IContract
    {
        private readonly SmartContract instance;

        /// <summary>
        /// Indicates whether the ISmartContractState fields have been set on the instance object yet.
        /// </summary>
        private bool initialized;

        public uint160 Address { get; }

        public Type Type { get; }

        public ISmartContractState State { get; }

        public Contract(SmartContract instance, Type type, uint160 address, ISmartContractState state)
        {
            this.instance = instance;
            this.State = state;
            this.Type = type;
            this.Address = address;
        }

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

        public IContractInvocationResult Invoke(string methodName, IReadOnlyList<object> parameters)
        {
            object[] invokeParams = parameters?.ToArray() ?? new object[0];

            Type[] types = invokeParams.Select(p => p.GetType()).ToArray();

            MethodInfo methodToInvoke = this.Type.GetMethod(methodName, types);

            if (methodToInvoke == null)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodDoesNotExist);

            // This should not happen without setting the appropriate binding flags
            if (methodToInvoke.IsConstructor)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodIsConstructor);

            // This should not happen without setting the appropriate binding flags
            if (methodToInvoke.IsPrivate)
                return ContractInvocationResult.Failure(ContractInvocationErrorType.MethodIsPrivate);

            // Restore the state of the instance
            if(!this.initialized)
                SetStateFields(this.instance, this.State);

            return this.InvokeInternal(methodToInvoke, invokeParams);
        }

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