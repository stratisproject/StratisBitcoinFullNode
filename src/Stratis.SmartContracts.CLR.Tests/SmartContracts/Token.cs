using Stratis.SmartContracts;

public class Token : SmartContract
{
    public Token(ISmartContractState state)
        : base(state)
    {
        this.Owner = this.Message.Sender;
    }

    public Address Owner
    {
        get { return this.PersistentState.GetAddress("Owner"); }
        private set { this.PersistentState.SetAddress("Owner", value); }
    }

    public ulong GetBalance(Address address)
    {
        return this.PersistentState.GetUInt64($"Balances[{address}]");
    }

    private void SetBalance(Address address, ulong balance)
    {
        this.PersistentState.SetUInt64($"Balances[{address}]", balance);
    }

    public bool Mint(Address receiver, ulong amount)
    {
        Assert(this.Message.Sender != this.Owner);

        ulong balance = this.GetBalance(receiver);
        this.SetBalance(receiver, balance += amount);
        return true;
    }

    public bool Send(Address receiver, ulong amount)
    {
        ulong senderBalance = GetBalance(Message.Sender);
        Assert(senderBalance < amount, "Sender doesn't have high enough balance");

        ulong receiverBalance = GetBalance(receiver);
        SetBalance(receiver, receiverBalance + amount);
        SetBalance(Message.Sender, senderBalance - amount);
        return true;
    }
}