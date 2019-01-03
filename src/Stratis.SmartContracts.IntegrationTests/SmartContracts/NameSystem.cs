using Stratis.SmartContracts;

/// <summary>
/// First-in: best-dressed.
/// </summary>
public class NameSystem : SmartContract
{
    public NameSystem(ISmartContractState state) : base(state)
    {
    }

    public Address GetOwner(string name)
    {
        return PersistentState.GetAddress($"Registry:{name}");
    }

    private void SetOwner(string name, Address owner)
    {
        PersistentState.SetAddress($"Registry:{name}", owner);
    }

    public void TakeName(string name)
    {
        Assert(GetOwner(name) == default(Address));
        SetOwner(name, Message.Sender);
    }
}