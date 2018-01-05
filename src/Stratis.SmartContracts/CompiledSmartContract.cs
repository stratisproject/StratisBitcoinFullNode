using Stratis.SmartContracts.Exceptions;
using System;
using System.Reflection;
using NBitcoin;

namespace Stratis.SmartContracts
{
    public abstract class CompiledSmartContract
    {
        protected Address Address { get; private set; }

        protected ulong Balance {
            get
            {
                throw new NotImplementedException();
                //return PersistentState.StateDb.GetBalance(Address.ToUint160());
            }
        }

        public ulong GasUsed { get; private set; }

        public CompiledSmartContract()
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            Address = Message.ContractAddress; // Note that this will get called before the derived constructor so Address will be set.
        }

        public void SpendGas(uint spend)
        {
            if (GasUsed +  spend > Message.GasLimit)
                throw new OutOfGasException("Went over gas limit of " + Message.GasLimit);

            GasUsed += spend;
        }

        protected object Call(Address addressTo, ulong amount, TransactionDetails transactionDetails = null)
        {
            var contractCode = PersistentState.StateDb.GetCode(addressTo.ToUint160());

            if (Balance < amount)
                throw new InsufficientBalanceException();

            // Handling balance

            //PersistentState.StateDb.SubtractBalance(Address.ToUint160(), amount);
            //PersistentState.StateDb.AddBalance(addressTo.ToUint160(), amount);

            if (contractCode != null && contractCode.Length > 0)
            {
                // Create the context to be injected into the block
                Address currentCallerAddress = Message.Sender;
                ulong currentCallValue = Message.Value;
                Message.Set(addressTo, this.Address, amount, Message.GasLimit);
                PersistentState.SetAddress(addressTo.ToUint160());

                // Initialise the assembly and contract object
                Assembly assembly = Assembly.Load(contractCode);
                Type type = assembly.GetType(transactionDetails.ContractTypeName);

                var contractObject = (CompiledSmartContract)Activator.CreateInstance(type);

                var methodToInvoke = type.GetMethod(transactionDetails.ContractMethodName);
                var result = methodToInvoke.Invoke(contractObject, transactionDetails.Parameters);

                // return context back to normal
                Message.Set(this.Address, currentCallerAddress, currentCallValue, Message.GasLimit);
                PersistentState.SetAddress(this.Address.ToUint160());
                return result;
            }

            // Should probably cost some gas to transfer but otherwise, complete
            return null;
        }
    }
}
