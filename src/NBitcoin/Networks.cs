using NBitcoin.Networks;

namespace NBitcoin
{
    public partial class Network
    {
        public static Network Main => NetworksContainer.GetNetwork("Main") ?? NetworksContainer.Register(new BitcoinMain());

        public static Network TestNet => NetworksContainer.GetNetwork("TestNet") ?? NetworksContainer.Register(new BitcoinTest());

        public static Network RegTest => NetworksContainer.GetNetwork("RegTest") ?? NetworksContainer.Register(new BitcoinRegTest());

        public static Network StratisMain => NetworksContainer.GetNetwork("StratisMain") ?? NetworksContainer.Register(new StratisMain());

        public static Network StratisTest => NetworksContainer.GetNetwork("StratisTest") ?? NetworksContainer.Register(new StratisTest());

        public static Network StratisRegTest => NetworksContainer.GetNetwork("StratisRegTest") ?? NetworksContainer.Register(new StratisRegTest());
    }
}
