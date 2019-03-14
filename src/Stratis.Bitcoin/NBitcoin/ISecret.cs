namespace Stratis.Bitcoin.NBitcoin
{
    public interface ISecret
    {
        Key PrivateKey
        {
            get;
        }
    }
}
