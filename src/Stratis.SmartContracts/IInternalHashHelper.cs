namespace Stratis.SmartContracts
{
    public interface IInternalHashHelper
    {
        byte[] Keccak256(byte[] toHash); 
    }
}
