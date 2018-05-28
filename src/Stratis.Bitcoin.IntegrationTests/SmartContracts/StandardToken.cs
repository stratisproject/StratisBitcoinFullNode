using System;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    public class StandardToken : SmartContract, IStandardToken, IMintableToken
    {
        public StandardToken(ISmartContractState smartContractState, ulong totalSupply) : base(smartContractState)
        {
            if (totalSupply == 0)
                throw new ArgumentException("Token supply must be greater than 0", nameof(totalSupply));
            this.Owner = this.Message.Sender;
            this.TotalSupply = totalSupply;
        }
        public ISmartContractMapping<ulong> Balances => this.PersistentState.GetMapping<ulong>(nameof(this.Balances));

        public ulong TotalSupply
        {
            get => this.PersistentState.GetObject<ulong>(nameof(this.TotalSupply));
            private set => this.PersistentState.SetObject(nameof(this.TotalSupply), value);
        }

        public bool HasMintingFinished
        {
            get => this.PersistentState.GetObject<bool>(nameof(this.HasMintingFinished));
            private set => this.PersistentState.SetObject(nameof(this.HasMintingFinished), value);
        }

        public Address Owner
        {
            get => this.PersistentState.GetObject<Address>(nameof(this.Owner));
            private set => this.PersistentState.SetObject(nameof(this.Owner), value);
        }

        public ulong GetBalance(Address address)
        {
            EnsureValidAddress(address);

            return this.Balances[address.Value];
        }

        public bool Transfer(Address to, ulong amountToTransfer)
        {
            return this.TransferFrom(this.Message.ContractAddress, to, amountToTransfer);
        }

        public bool Mint(Address to, ulong amountToMint)
        {
            this.EnsureOwnerExecution();
            if (this.HasMintingFinished)
            {
                return false;
            }

            checked
            {
                this.TotalSupply += amountToMint;
                this.Balances[to.Value] += amountToMint;
                return true;
            }
        }

        public void FinishMinting()
        {
            this.EnsureOwnerExecution();
            this.HasMintingFinished = true;
        }

        public bool TransferFrom(Address from, Address to, ulong amountToTransfer)
        {
            EnsureValidAddress(from, nameof(from));
            EnsureValidAddress(to, nameof(to));

            checked
            {
                if (this.Balances[from.Value] < amountToTransfer)
                {
                    throw new ApplicationException($"Insufficient funds in {from} to transfer {amountToTransfer}");
                }

                if (amountToTransfer > this.TotalSupply)
                {
                    throw new ApplicationException($"Amount to transfer exceeds total supply of {this.TotalSupply}");
                }

                this.Balances[from.Value] -= amountToTransfer;
                this.Balances[to.Value] += amountToTransfer;

                return true;
            }
        }

        private void EnsureOwnerExecution()
        {
            if (this.Message.Sender.Value != this.Owner.Value)
            {
                throw new ApplicationException("Forbidden");
            }
        }

        private static void EnsureValidAddress(Address address, string paramName = null)
        {
            if (string.IsNullOrWhiteSpace(address.Value))
            {
                throw new ApplicationException($"Invalid {(string.IsNullOrEmpty(paramName) ? string.Empty : $"'{paramName}' ")}address");
            }
        }
    }
}
