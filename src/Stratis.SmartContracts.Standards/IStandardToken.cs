namespace Stratis.SmartContracts.Standards
{
    public interface IStandardToken
    {
        ulong TotalSupply { get; }

        ulong GetBalance(Address address);

        bool Transfer(Address to, ulong value);
    }
}
