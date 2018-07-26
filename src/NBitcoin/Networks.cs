using NBitcoin.NetworkDefinitions;

namespace NBitcoin
{
    public static class Networks
    {
        public static Network Main => NetworksContainer.GetNetwork("Main") ?? NetworksContainer.Register(new BitcoinMain());

        public static Network TestNet => NetworksContainer.GetNetwork("TestNet") ?? NetworksContainer.Register(new BitcoinTest());

        public static Network RegTest => NetworksContainer.GetNetwork("RegTest") ?? NetworksContainer.Register(new BitcoinRegTest());
    }
}