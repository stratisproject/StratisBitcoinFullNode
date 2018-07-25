using NBitcoin;

namespace Stratis.Bitcoin.Networks
{
    public static class StratisNetworks
    {
        public static Network StratisMain => NetworksContainer.GetNetwork("StratisMain") ?? NetworksContainer.Register(new StratisMain());

        public static Network StratisTest => NetworksContainer.GetNetwork("StratisTest") ?? NetworksContainer.Register(new StratisTest());

        public static Network StratisRegTest => NetworksContainer.GetNetwork("StratisRegTest") ?? NetworksContainer.Register(new StratisRegTest());
    }
}