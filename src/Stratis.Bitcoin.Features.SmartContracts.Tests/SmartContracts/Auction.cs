using Stratis.SmartContracts;

public class Auction : SmartContract
{
    public Address Owner
    {
        get
        {
            return this.PersistentState.GetObject<Address>("Owner");
        }
        set
        {
            this.PersistentState.SetObject<Address>("Owner", value);
        }
    }

    public ulong EndBlock
    {
        get
        {
            return this.PersistentState.GetObject<ulong>("EndBlock");
        }
        set
        {
            this.PersistentState.SetObject<ulong>("EndBlock", value);
        }
    }

    public Address HighestBidder
    {
        get
        {
            return this.PersistentState.GetObject<Address>("HighestBidder");
        }
        set
        {
            this.PersistentState.SetObject<Address>("HighestBidder", value);
        }
    }

    public ulong HighestBid
    {
        get
        {
            return this.PersistentState.GetObject<ulong>("HighestBid");
        }
        set
        {
            this.PersistentState.SetObject<ulong>("HighestBid", value);
        }
    }

    public bool HasEnded
    {
        get
        {
            return this.PersistentState.GetObject<bool>("HasEnded");
        }
        set
        {
            this.PersistentState.SetObject<bool>("HasEnded", value);
        }
    }

    public ISmartContractMapping<ulong> ReturnBalances
    {
        get
        {
            return this.PersistentState.GetMapping<ulong>("ReturnBalances");
        }
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
            this.ReturnBalances[this.HighestBidder] = this.HighestBid;
        }
        this.HighestBidder = this.Message.Sender;
        this.HighestBid = this.Message.Value;
    }

    public bool Withdraw()
    {
        ulong amount = this.ReturnBalances[this.Message.Sender];
        Assert(amount > 0);
        this.ReturnBalances[this.Message.Sender] = 0;
        ITransferResult transferResult = TransferFunds(this.Message.Sender, amount);
        if (!transferResult.Success)
            this.ReturnBalances[this.Message.Sender] = amount;
        return transferResult.Success;
    }

    public void AuctionEnd()
    {
        Assert(this.Block.Number >= this.EndBlock);
        Assert(!this.HasEnded);
        this.HasEnded = true;
        TransferFunds(this.Owner, this.HighestBid);
    }
}