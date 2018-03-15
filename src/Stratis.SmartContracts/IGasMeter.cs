namespace Stratis.SmartContracts
{
    public interface IGasMeter
    {
        Gas GasLimit { get; }
        Gas AvailableGas { get; }
        Gas ConsumedGas { get; }
        void Spend(Gas spend);
    }
}