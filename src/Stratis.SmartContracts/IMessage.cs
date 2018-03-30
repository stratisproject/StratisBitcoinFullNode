namespace Stratis.SmartContracts
{
    public interface IMessage
    {
        Address ContractAddress { get; }
        Address Sender { get; }
        Gas GasLimit { get; }
        ulong Value { get; }
    }
}