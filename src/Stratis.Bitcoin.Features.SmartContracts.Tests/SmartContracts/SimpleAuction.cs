using Stratis.SmartContracts;

public class SimpleAuction : SmartContract
{
    public SimpleAuction(ISmartContractState smartContractState) : base(smartContractState)
    {
        this.PendingReturns = this.PersistentState.GetMapping<ulong>("PendingReturns");
    }

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

    public ulong AuctionEndBlock
    {
        get
        {
            return this.PersistentState.GetObject<ulong>("AuctionEndBlock");
        }
        set
        {
            this.PersistentState.SetObject<ulong>("AuctionEndBlock", value);
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

    public ISmartContractMapping<ulong> PendingReturns { get; set; }

    public bool HasEnded
    {
        get
        {
            return this.PersistentState.GetObject<bool>("hasended");
        }
        set
        {
            this.PersistentState.SetObject<bool>("hasended", value);
        }
    }

    [SmartContractInit]
    public void Initialise(ulong biddingBlocks)
    {
        this.Owner = this.Message.Sender;
        this.AuctionEndBlock = this.Block.Number + biddingBlocks;
        this.HasEnded = false;
    }

    public void Bid()
    {
        Assert(this.Block.Number < this.AuctionEndBlock);
        Assert(this.Message.Value > this.HighestBid);
        if (this.HighestBid > 0)
        {
            this.PendingReturns[this.HighestBidder] = this.HighestBid;
        }
        this.HighestBidder = this.Message.Sender;
        this.HighestBid = this.Message.Value;
    }

    public bool Withdraw()
    {
        ulong amount = this.PendingReturns[this.Message.Sender];
        Assert(amount > 0);
        this.PendingReturns[this.Message.Sender] = 0;
        ITransferResult transferResult = TransferFunds(this.Message.Sender, amount);
        if (!transferResult.Success)
            this.PendingReturns[this.Message.Sender] = amount;
        return transferResult.Success;
    }

    public void AuctionEnd()
    {
        Assert(this.Block.Number >= this.AuctionEndBlock);
        Assert(!this.HasEnded);
        this.HasEnded = true;
        TransferFunds(this.Owner, this.HighestBid);
    }
}