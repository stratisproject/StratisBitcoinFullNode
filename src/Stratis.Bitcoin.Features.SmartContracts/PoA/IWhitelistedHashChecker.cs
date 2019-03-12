namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public interface IWhitelistedHashChecker
    {
        bool CheckHashWhitelisted(byte[] hash);
    }
}