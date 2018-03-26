using Stratis.SmartContracts;

public class SimpleAuction : SmartContract
{
    public SimpleAuction(ISmartContractState smartContractState) : base(smartContractState)
    {
        PendingReturns = PersistentState.GetMapping<ulong>("PendingReturns");
    }

    public Address Owner
    {
        get
        {
            return PersistentState.GetObject<Address>("Owner");
        }
        set
        {
            PersistentState.SetObject<Address>("Owner", value);
        }
    }

    public ulong AuctionEndBlock {
        get
        {
            return PersistentState.GetObject<ulong>("AuctionEndBlock");
        }
        set
        {
            PersistentState.SetObject<ulong>("AuctionEndBlock", value);
        }
    }

    public Address HighestBidder
    {
        get
        {
            return PersistentState.GetObject<Address>("HighestBidder");
        }
        set
        {
            PersistentState.SetObject<Address>("HighestBidder", value);
        }
    }

    public ulong HighestBid
    {
        get
        {
            return PersistentState.GetObject<ulong>("HighestBid");
        }
        set
        {
            PersistentState.SetObject<ulong>("HighestBid", value);
        }
    }

    public ISmartContractMapping<ulong> PendingReturns { get; set; }

    public bool HasEnded
    {
        get
        {
            return PersistentState.GetObject<bool>("hasended");
        }
        set
        {
            PersistentState.SetObject<bool>("hasended", value);
        }
    }

    [SmartContractInit]
    public void Initialise(ulong biddingBlocks)
    {
        Owner = Message.Sender;
        AuctionEndBlock = Block.Number + biddingBlocks;
        HasEnded = false;
    }

    public void Bid()
    {
        Assert(Block.Number < AuctionEndBlock);
        Assert(Message.Value > HighestBid);
        if (HighestBid > 0)
        {
            PendingReturns[HighestBidder] = HighestBid;
        }
        HighestBidder = Message.Sender;
        HighestBid = Message.Value;
    }

    public bool Withdraw()
    {
        ulong amount = PendingReturns[Message.Sender];
        Assert(amount > 0);
        PendingReturns[Message.Sender] = 0;
        ITransferResult transferResult = TransferFunds(Message.Sender, amount);
        if (!transferResult.Success)
            PendingReturns[Message.Sender] = amount;
        return transferResult.Success;
    }

    public void AuctionEnd()
    {
        Assert(Block.Number >= AuctionEndBlock);
        Assert(!HasEnded);
        HasEnded = true;
        TransferFunds(Owner, HighestBid);
    }

}
