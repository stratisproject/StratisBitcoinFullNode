using Stratis.SmartContracts;

public class Auction : SmartContract
{
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

    public ulong EndBlock
    {
        get
        {
            return PersistentState.GetObject<ulong>("EndBlock");
        }
        set
        {
            PersistentState.SetObject<ulong>("EndBlock", value);
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

    public bool HasEnded
    {
        get
        {
            return PersistentState.GetObject<bool>("HasEnded");
        }
        set
        {
            PersistentState.SetObject<bool>("HasEnded", value);
        }
    }

    public ISmartContractMapping<ulong> ReturnBalances
    {
        get
        {
            return PersistentState.GetMapping<ulong>("ReturnBalances");
        }
    }

    public Auction(ISmartContractState smartContractState, ulong durationBlocks)
    : base(smartContractState)
    {
        Owner = Message.Sender;
        EndBlock = Block.Number + durationBlocks;
        HasEnded = false;
    }

    public void Bid()
    {
        Assert(Block.Number < EndBlock);
        Assert(Message.Value > HighestBid);
        if (HighestBid > 0)
        {
            ReturnBalances[HighestBidder] = HighestBid;
        }
        HighestBidder = Message.Sender;
        HighestBid = Message.Value;
    }

    public bool Withdraw()
    {
        ulong amount = ReturnBalances[Message.Sender];
        Assert(amount > 0);
        ReturnBalances[Message.Sender] = 0;
        ITransferResult transferResult = TransferFunds(Message.Sender, amount);
        if (!transferResult.Success)
            ReturnBalances[Message.Sender] = amount;
        return transferResult.Success;
    }

    public void AuctionEnd()
    {
        Assert(Block.Number >= EndBlock);
        Assert(!HasEnded);
        HasEnded = true;
        TransferFunds(Owner, HighestBid);
    }
}