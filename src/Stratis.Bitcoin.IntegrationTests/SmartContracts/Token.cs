using Stratis.SmartContracts;

[Deploy]
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

    public ISmartContractMapping<ulong> Balances
    {
        get => this.PersistentState.GetUInt64Mapping("Balances");
    }

    public bool Mint(Address receiver, ulong amount)
    {
        Assert(this.Message.Sender != this.Owner);

        amount = amount + this.Block.Number;
        this.Balances[receiver.ToString()] += amount;
        return true;
    }

    public bool Send(Address receiver, ulong amount)
    {
        Assert(this.Balances.Get(this.Message.Sender.ToString()) < amount);

        this.Balances[receiver.ToString()] += amount;
        this.Balances[this.Message.Sender.ToString()] -= amount;
        return true;
    }
}