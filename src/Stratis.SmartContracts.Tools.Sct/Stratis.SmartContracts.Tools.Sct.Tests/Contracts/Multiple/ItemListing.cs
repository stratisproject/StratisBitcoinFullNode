using Stratis.SmartContracts;

public class ItemListing : SmartContract
{
    public ItemListing(ISmartContractState state, string itemName, ulong itemPrice, Address seller, Address parent, Address partyA, Address partyB)
        : base(state)
    {
        this.ItemName = itemName;
        this.ItemPrice = itemPrice;
        this.Seller = seller;
        this.ParentContract = parent;
        this.PartyA = partyA;
        this.PartyB = partyB;

        this.State = (uint)StateType.ItemAvailable;
    }

    public enum StateType : uint
    {
        ItemAvailable = 0,
        ItemSold = 1
    }

    public uint State
    {
        get => this.PersistentState.GetUInt32(nameof(State));
        private set => this.PersistentState.SetUInt32(nameof(State), value);
    }

    public Address Seller
    {
        get => this.PersistentState.GetAddress(nameof(Seller));
        private set => this.PersistentState.SetAddress(nameof(Seller), value);
    }

    public Address Buyer
    {
        get => this.PersistentState.GetAddress(nameof(Buyer));
        private set => this.PersistentState.SetAddress(nameof(Buyer), value);
    }

    public Address ParentContract
    {
        get => this.PersistentState.GetAddress(nameof(ParentContract));
        private set => this.PersistentState.SetAddress(nameof(ParentContract), value);
    }

    public string ItemName
    {
        get => this.PersistentState.GetString(nameof(ItemName));
        private set => this.PersistentState.SetString(nameof(ItemName), value);
    }

    public ulong ItemPrice
    {
        get => this.PersistentState.GetUInt64(nameof(ItemPrice));
        private set => this.PersistentState.SetUInt64(nameof(ItemPrice), value);
    }

    public Address PartyA
    {
        get => this.PersistentState.GetAddress(nameof(PartyA));
        private set => this.PersistentState.SetAddress(nameof(PartyA), value);
    }

    public Address PartyB
    {
        get => this.PersistentState.GetAddress(nameof(PartyB));
        private set => this.PersistentState.SetAddress(nameof(PartyB), value);
    }

    public void BuyItem()
    {
        // Ensure that the buyer is not the seller.
        Assert(Seller != Message.Sender);

        var hasBalance = this.Call(this.ParentContract, 0, "HasBalance", new object[] { Message.Sender, ItemPrice });

        Assert(hasBalance.Success && (bool)hasBalance.ReturnValue);

        Buyer = Message.Sender;

        var updateBalanceResult = this.Call(this.ParentContract, 0, "UpdateBalance", new object[] { Seller, Message.Sender, ItemPrice });

        Assert(updateBalanceResult.Success);

        State = (uint)StateType.ItemSold;
    }
}