using System;
using Stratis.SmartContracts;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.SmartContracts
{
    public class Token : SmartContract
    {
        public Token(SmartContractState state) 
            : base(state)
        {
            this.Balances = PersistentState.GetMapping<Address, ulong>();
        }

        public Address Owner
        {
            get { return PersistentState.GetObject<Address>("Owner"); }
            private set { PersistentState.SetObject("Owner", value); }
        }

        public SmartContractMapping<Address, ulong> Balances { get; set; }

        [SmartContractInit]
        public void Init()
        {
            this.Owner = Message.Sender;
        }

        public bool Mint(Address receiver, ulong amount)
        {
            if (Message.Sender != this.Owner)
                throw new Exception("Sender of this message is not the owner. " + this.Owner.ToString() + " vs " +
                                    Message.Sender.ToString());

            amount = amount + Block.Number;
            this.Balances[receiver] += amount;
            return true;
        }

        public bool Send(Address receiver, ulong amount)
        {
            if (this.Balances.Get(Message.Sender) < amount)
                throw new Exception("Sender doesn't have high enough balance");

            this.Balances[receiver] += amount;
            this.Balances[Message.Sender] -= amount;
            return true;
        }

        public void GasTest()
        {
            ulong test = 1;
            while (true)
            {
                test++;
                test--;
            }
        }
    }
}