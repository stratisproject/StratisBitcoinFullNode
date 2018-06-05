using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection.Lifecycle
{
    public static class SmartContractRestorer
    {
        /// <summary>
        /// Restores a smart contract and sets its state fields without invoking the constructor
        /// </summary>
        public static LifecycleResult Restore(Type type, ISmartContractState state)
        {
            try
            {
                var smartContract = (SmartContract)FormatterServices.GetSafeUninitializedObject(type);

                SetStateFields(smartContract, state);

                return new LifecycleResult(smartContract);
            }
            catch (Exception e)
            {
                return new LifecycleResult(e);
            }
        }

        private static void SetStateFields(SmartContract smartContract, ISmartContractState contractState)
        {
            FieldInfo[] fields = typeof(SmartContract)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

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