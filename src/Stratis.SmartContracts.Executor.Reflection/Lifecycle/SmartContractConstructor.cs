using System;

namespace Stratis.SmartContracts.Executor.Reflection.Lifecycle
{
    public static class SmartContractConstructor
    {
        /// <summary>
        /// Invokes a smart contract's constructor with the provided parameters
        /// </summary>
        public static LifecycleResult Construct(Type type, ISmartContractState state, params object[] parameters)
        {
            object[] newParams;

            if (parameters != null && parameters.Length > 0)
            {
                newParams = new object[parameters.Length + 1];
                newParams[0] = state;

                Array.Copy(parameters, 0, newParams, 1, parameters.Length);
            }
            else
            {
                newParams = new object[] { state };
            }

            try
            {
                var smartContract = (SmartContract)Activator.CreateInstance(type, newParams);
                return new LifecycleResult(smartContract);
            }
            catch (Exception e)
            {
                return new LifecycleResult(e);
            }
        }
    }
}