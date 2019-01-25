using Stratis.SmartContracts;

[Deploy]
public class Auction : SmartContract
{
    public Address Owner
    {
        get
        {
            return this.PersistentState.GetAddress("Owner");
        }
        set
        {
            this.PersistentState.SetAddress("Owner", value);
        }
    }

    public ulong EndBlock
    {
        get
        {
            return this.PersistentState.GetUInt64("EndBlock");
        }
        set
        {
            this.PersistentState.SetUInt64("EndBlock", value);
        }
    }

    public Address HighestBidder
    {
        get
        {
            return this.PersistentState.GetAddress("HighestBidder");
        }
        set
        {
            this.PersistentState.SetAddress("HighestBidder", value);
        }
    }

    public ulong HighestBid
    {
        get
        {
            return this.PersistentState.GetUInt64("HighestBid");
        }
        set
        {
            this.PersistentState.SetUInt64("HighestBid", value);
        }
    }

    public bool HasEnded
    {
        get
        {
            return this.PersistentState.GetBool("HasEnded");
        }
        set
        {
            this.PersistentState.SetBool("HasEnded", value);
        }
    }

    public ulong GetBalance(Address address)
    {
        return this.PersistentState.GetUInt64($"Balances[{address}]");
    }

    private void SetBalance(Address address, ulong balance)
    {
        this.PersistentState.SetUInt64($"Balances[{address}]", balance);
    }

    public Auction(ISmartContractState smartContractState, ulong durationBlocks)
        : base(smartContractState)
    {
        this.Owner = this.Message.Sender;
        this.EndBlock = this.Block.Number + durationBlocks;
        this.HasEnded = false;
    }

    public void Bid()
    {
        Assert(this.Block.Number < this.EndBlock);
        Assert(this.Message.Value > this.HighestBid);
        if (this.HighestBid > 0)
        {
            // NOTE: There is a bug here - the return balance will overwrite the previous balance. Balances should be cumulative.
            SetBalance(this.Message.Sender, this.HighestBid);
        }
        this.HighestBidder = this.Message.Sender;
        this.HighestBid = this.Message.Value;
    }

    public bool Withdraw()
    {
        ulong amount = GetBalance(this.Message.Sender);
        Assert(amount > 0);
        SetBalance(this.Message.Sender, 0);
        ITransferResult transferResult = Transfer(this.Message.Sender, amount);
        if (!transferResult.Success)
            this.SetBalance(this.Message.Sender, amount);
        return transferResult.Success;
    }

    public void AuctionEnd()
    {
        Assert(this.Block.Number >= this.EndBlock);
        Assert(!this.HasEnded);
        this.HasEnded = true;
        Transfer(this.Owner, this.HighestBid);
    }
}