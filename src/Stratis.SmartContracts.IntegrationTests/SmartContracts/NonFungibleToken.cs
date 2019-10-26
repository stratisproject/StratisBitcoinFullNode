using Stratis.SmartContracts;

public class StratisCollectible : SmartContract
{
    public ulong BalanceOf(Address owner)
    {
        return PersistentState.GetUInt64($"Balance:{owner}");
    }

    private void SetBalance(Address address, ulong balance)
    {
        PersistentState.SetUInt64($"Balance:{address}", balance);
    }

    public Address OwnerOf(ulong tokenId)
    {
        return PersistentState.GetAddress($"Token{tokenId}");
    }

    private void SetOwner(ulong tokenId, Address address)
    {
        PersistentState.SetAddress($"Token{tokenId}", address);
    }

    public StratisCollectible(ISmartContractState state) : base(state)
    {
        Assert(Message.Value == 0); // Don't want to lose any funds

        // Assign 3 NFTs to the creator.
        SetOwner(1, Message.Sender);
        SetOwner(2, Message.Sender);
        SetOwner(3, Message.Sender);
        SetBalance(Message.Sender, 3);
    }

    /// <summary>
    /// Note that this does not check the receiver is able to use the token (in case they are a contract without the necessary interface)
    /// </summary>
    public void TransferFrom(Address from, Address to, ulong tokenId)
    {
        Assert(Message.Value == 0); // Don't want to lose any funds
        Assert(from == Message.Sender); // Until we implement approved list
        Assert(OwnerOf(tokenId) == Message.Sender);
        Assert(from != to);

        AddTokenTo(to, tokenId);
        RemoveTokenFrom(from);

        Log(new Transfer{From = from, To = to, TokenId = tokenId});
    }

    private void AddTokenTo(Address to, ulong tokenId)
    {
        SetBalance(to, BalanceOf(to) + 1);
        SetOwner(tokenId, to);
    }

    private void RemoveTokenFrom(Address from)
    {
        SetBalance(from, BalanceOf(from) - 1);
    }

    public struct Transfer
    {
        [Index]
        public ulong TokenId;

        [Index]
        public Address From;

        [Index]
        public Address To;
    }

}