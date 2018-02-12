using System;
using Stratis.SmartContracts;
using System.Linq;

public class Token : SmartContract
{

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

    public SmartContractMapping<Address, ulong> Balances { get; set; } = PersistentState.GetMapping<Address, ulong>();

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
        Balances[receiver] += amount;
        return true;
    }

    public bool Send(Address receiver, ulong amount)
    {
        if (Balances.Get(Message.Sender) < amount)
            throw new Exception("Sender doesn't have high enough balance");

        Balances[receiver] += amount;
        Balances[Message.Sender] -= amount;
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