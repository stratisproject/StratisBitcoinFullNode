using Stratis.SmartContracts.Exceptions;
using System;
using System.Reflection;
using NBitcoin;

namespace Stratis.SmartContracts
{
    public abstract class SmartContract
    {
        protected Address Address { get; private set; }

        protected ulong Balance {
            get
            {
                throw new NotImplementedException();
            }
        }

        public ulong GasUsed { get; private set; }

        public SmartContract()
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            this.Address = Message.ContractAddress; // Note that this will get called before the derived constructor so Address will be set.
        }

        /// <summary>
        /// Expends the given amount of gas. If this takes the spent gas over the entered limit, throw an OutOfGasException
        /// </summary>
        /// <param name="spend"></param>
        public void SpendGas(uint spend)
        {
            if (this.GasUsed +  spend > Message.GasLimit)
                throw new OutOfGasException("Went over gas limit of " + Message.GasLimit);

            this.GasUsed += spend;
        }

        /// <summary>
        /// Work in progress. Will be used to send transactions to other addresses or contracts.
        /// </summary>
        /// <param name="addressTo"></param>
        /// <param name="amount"></param>
        protected void Transfer(Address addressTo, ulong amount)
        {
            PersistentState.StateDb.TransferBalance(this.Address.ToUint160(), addressTo.ToUint160(), amount);
        }

        /// <summary>
        /// Work in progress. Will be used to send transactions to other addresses or contracts.
        /// </summary>
        /// <param name="addressTo"></param>
        /// <param name="amount"></param>
        /// <param name="transactionDetails"></param>
        /// <returns></returns>
        protected object Call(Address addressTo, ulong amount, TransactionDetails transactionDetails = null)
        {
            throw new NotImplementedException();
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

                var contractObject = (SmartContract)Activator.CreateInstance(type);

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
