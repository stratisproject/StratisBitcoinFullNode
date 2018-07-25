using NBitcoin.NetworkDefinitions;

namespace NBitcoin
{
    public static class Networks
    {
        public static Network Main => NetworksContainer.GetNetwork("Main") ?? NetworksContainer.Register(new BitcoinMain());

        public static Network TestNet => NetworksContainer.GetNetwork("TestNet") ?? NetworksContainer.Register(new BitcoinTest());

        public static Network RegTest => NetworksContainer.GetNetwork("RegTest") ?? NetworksContainer.Register(new BitcoinRegTest());

        public static Network StratisMain => NetworksContainer.GetNetwork("StratisMain") ?? NetworksContainer.Register(new StratisMain());

        public static Network StratisTest => NetworksContainer.GetNetwork("StratisTest") ?? NetworksContainer.Register(new StratisTest());

        public static Network StratisRegTest => NetworksContainer.GetNetwork("StratisRegTest") ?? NetworksContainer.Register(new StratisRegTest());

        public static Network CityMain => NetworksContainer.GetNetwork("CityMain") ?? NetworksContainer.Register(new CityMain());

        public static Network CityTest => NetworksContainer.GetNetwork("CityTest") ?? NetworksContainer.Register(new CityTest());

        public static Network CityRegTest => NetworksContainer.GetNetwork("CityRegTest") ?? NetworksContainer.Register(new CityRegTest());
    }
}