using System;
using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Standards
{
    /// <summary>
    /// TODO: investigare improvements for Assert checks
    /// </summary>
    public class StandardToken : SmartContract, IStandardToken
    {
        private readonly ISmartContractMapping<ulong> balances;
        private readonly ISmartContractMapping<ISmartContractMapping<ulong>> allowed;

        public StandardToken(ISmartContractState smartContractState, ulong totalSupply) : base(smartContractState)
        {
            if (totalSupply == 0)
                throw new ArgumentException("Token supply must be greater than 0", nameof(totalSupply));
            this.balances = this.PersistentState.GetMapping<ulong>("Balances");
            this.allowed = this.PersistentState.GetMapping<ISmartContractMapping<ulong>>("Allowed");
            this.Owner = this.Message.Sender;
            this.TotalSupply = totalSupply;
        }

        public ulong TotalSupply
        {
            get => this.PersistentState.GetObject<ulong>(nameof(this.TotalSupply));
            private set => this.PersistentState.SetObject(nameof(this.TotalSupply), value);
        }

        public Address Owner
        {
            get => this.PersistentState.GetObject<Address>(nameof(this.Owner));
            private set => this.PersistentState.SetObject(nameof(this.Owner), value);
        }

        public ulong GetBalance(Address address)
        {
            this.Assert(string.IsNullOrWhiteSpace(address.Value));

            return this.balances[address.Value];
        }

        public bool Transfer(Address to, ulong amountToTransfer)
        {
            return this.TransferFrom(this.Message.ContractAddress, to, amountToTransfer);
        }

        public bool TransferFrom(Address from, Address to, ulong amountToTransfer)
        {
            this.Assert(string.IsNullOrWhiteSpace(from.Value));
            this.Assert(string.IsNullOrWhiteSpace(to.Value));

            checked
            {   
                if (this.balances[from.Value] < amountToTransfer)
                {
                    throw new StandardTokenValidationException($"Insufficient funds in {from} to transfer {amountToTransfer}");
                }

                this.balances[from.Value] -= amountToTransfer;
                this.balances[to.Value] += amountToTransfer;

                return true;
            }
        }

        public ulong GetAllowance(Address owner, Address spender)
        {
            this.Assert(string.IsNullOrWhiteSpace(owner.Value));
            this.Assert(string.IsNullOrWhiteSpace(spender.Value));

            return this.allowed[owner.Value][spender.Value];
        }

        public bool Approve(Address sender, ulong amountToApprove)
        {
            Guard.AgainstInvalidAddress(sender, paramName: nameof(sender));

            this.allowed[this.Owner.Value][sender.Value] = amountToApprove;
            return true;
        }
    }
}
