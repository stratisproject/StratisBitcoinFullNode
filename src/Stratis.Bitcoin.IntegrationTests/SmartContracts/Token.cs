using System;
using Stratis.SmartContracts;

public class Token : SmartContract
{
    public Token(ISmartContractState state) 
        : base(state)
    {
        Balances = PersistentState.GetMapping<ulong>("Balances");
    }

    public Address Owner
    {
        get
        {
            return PersistentState.GetObject<Address>("Owner");
        }
        private set
        {
            PersistentState.SetObject("Owner", value);
        }
    }

    public ISmartContractMapping<ulong> Balances { get; }

    [SmartContractInit]
    public void Init()
    {
        Owner = Message.Sender;
    }

    public bool Mint(Address receiver, ulong amount)
    {
        if (Message.Sender != Owner)
            throw new Exception("Sender of this message is not the owner. " + Owner.ToString() + " vs " + Message.Sender.ToString());

        amount = amount + Block.Number;
        Balances[receiver.ToString()] += amount;
        return true;
    }

    public bool Send(Address receiver, ulong amount)
    {
        if (Balances.Get(Message.Sender.ToString()) < amount)
            throw new Exception("Sender doesn't have high enough balance");

        Balances[receiver.ToString()] += amount;
        Balances[Message.Sender.ToString()] -= amount;
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