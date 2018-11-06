using NBitcoin;

namespace Stratis.Sidechains.Networks
{
    public static class ApexNetworks
    {
        public static NetworksSelector Apex
        {
            get
            {
                return new NetworksSelector(() => new ApexMain(), () => new ApexTest(), () => new ApexRegTest());
            }
        }
    }
}
