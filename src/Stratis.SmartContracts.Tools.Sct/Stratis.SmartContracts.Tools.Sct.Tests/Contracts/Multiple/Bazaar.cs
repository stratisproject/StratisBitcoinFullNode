using Stratis.SmartContracts;

[Deploy]
public class Bazaar : SmartContract
{
    public Bazaar(ISmartContractState smartContractState, Address partyA, ulong balancePartyA, Address partyB, ulong balancePartyB)
        : base(smartContractState)
    {
        Assert(partyA != partyB);

        this.BazaarMaintainer = Message.Sender;
        this.PartyA = partyA;
        this.BalancePartyA = balancePartyA;
        this.PartyB = partyB;
        this.BalancePartyB = balancePartyB;

        this.State = (uint)StateEnum.PartyProvisioned;
    }

    public enum StateEnum : uint
    {
        PartyProvisioned = 0,
        ItemListed = 1,
        CurrentSaleFinalized = 2
    }

    public uint State
    {
        get => this.PersistentState.GetUInt32(nameof(State));
        private set => this.PersistentState.SetUInt32(nameof(State), value);
    }

    public Address PartyA
    {
        get => this.PersistentState.GetAddress(nameof(PartyA));
        private set => this.PersistentState.SetAddress(nameof(PartyA), value);
    }

    public ulong BalancePartyA
    {
        get => this.PersistentState.GetUInt64(nameof(BalancePartyA));
        private set => this.PersistentState.SetUInt64(nameof(BalancePartyA), value);
    }

    public Address PartyB
    {
        get => this.PersistentState.GetAddress(nameof(PartyB));
        private set => this.PersistentState.SetAddress(nameof(PartyB), value);
    }

    public ulong BalancePartyB
    {
        get => this.PersistentState.GetUInt64(nameof(BalancePartyB));
        private set => this.PersistentState.SetUInt64(nameof(BalancePartyB), value);
    }

    public Address BazaarMaintainer
    {
        get => this.PersistentState.GetAddress(nameof(BazaarMaintainer));
        private set => this.PersistentState.SetAddress(nameof(BazaarMaintainer), value);
    }

    public Address CurrentSeller
    {
        get => this.PersistentState.GetAddress(nameof(CurrentSeller));
        private set => this.PersistentState.SetAddress(nameof(CurrentSeller), value);
    }

    public Address CurrentItemListing
    {
        get => this.PersistentState.GetAddress(nameof(CurrentItemListing));
        private set => this.PersistentState.SetAddress(nameof(CurrentItemListing), value);
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

    public bool HasBalance(Address buyer, ulong itemPrice)
    {
        Assert(buyer == PartyA || buyer == PartyB);

        if (buyer == PartyA)
        {
            return BalancePartyA >= itemPrice;
        }

        if (buyer == PartyB)
        {
            return BalancePartyB >= itemPrice;
        }

        return false;
    }

    public void UpdateBalance(Address seller, Address buyer, ulong price)
    {
        Assert(Message.Sender == CurrentItemListing);

        IncreaseBalance(seller, price);
        DecreaseBalance(buyer, price);

        State = (uint)StateEnum.CurrentSaleFinalized;
    }

    private void IncreaseBalance(Address party, ulong amount)
    {
        if (party == PartyA)
        {
            checked
            {
                BalancePartyA += amount;
            }
        }

        if (party == PartyB)
        {
            checked
            {
                BalancePartyB += amount;
            }
        }
    }

    private void DecreaseBalance(Address party, ulong amount)
    {
        if (party == PartyA)
        {
            checked
            {
                BalancePartyA -= amount;
            }
        }

        if (party == PartyB)
        {
            checked
            {
                BalancePartyB -= amount;
            }
        }
    }

    public void ListItem(string itemName, ulong itemPrice)
    {
        Assert(Message.Sender == PartyA || Message.Sender == PartyB);

        var createResult = Create<ItemListing>(0, new object[] { itemName, itemPrice, Message.Sender, this.Address, PartyA, PartyB });

        Assert(createResult.Success);

        CurrentItemListing = createResult.NewContractAddress;

        State = (uint)StateEnum.ItemListed;
    }
}