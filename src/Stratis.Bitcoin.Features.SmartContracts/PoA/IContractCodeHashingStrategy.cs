namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public interface IContractCodeHashingStrategy
    {
        byte[] Hash(byte[] data);
    }
}