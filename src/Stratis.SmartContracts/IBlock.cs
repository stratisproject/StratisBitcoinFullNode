namespace Stratis.SmartContracts
{
    public interface IBlock
    {
        Address Coinbase { get; }
        ulong Number { get; }
    }
}