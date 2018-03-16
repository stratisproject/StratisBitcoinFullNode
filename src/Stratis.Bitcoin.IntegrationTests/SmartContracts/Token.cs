using System;
using Stratis.SmartContracts;

public class Token : SmartContract
{
    public Token(ISmartContractState state)
        : base(state)
    {
        this.Balances = this.PersistentState.GetMapping<ulong>("Balances");
    }

    public Address Owner
    {
        get
        {
            return this.PersistentState.GetObject<Address>("Owner");
        }
        private set
        {
            this.PersistentState.SetObject("Owner", value);
        }
    }

    public ISmartContractMapping<ulong> Balances { get; }

    [SmartContractInit]
    public void Init()
    {
        this.Owner = this.Message.Sender;
    }

    public bool Mint(Address receiver, ulong amount)
    {
        if (this.Message.Sender != this.Owner)
            throw new Exception("Sender of this message is not the owner. " + this.Owner.ToString() + " vs " + this.Message.Sender.ToString());

        amount = amount + this.Block.Number;
        this.Balances[receiver.ToString()] += amount;
        return true;
    }

    public bool Send(Address receiver, ulong amount)
    {
        if (this.Balances.Get(this.Message.Sender.ToString()) < amount)
            throw new Exception("Sender doesn't have high enough balance");

        this.Balances[receiver.ToString()] += amount;
        this.Balances[this.Message.Sender.ToString()] -= amount;
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