using NBitcoin.DataEncoders;
using NBitcoin.Networks;

namespace NBitcoin
{
    public partial class Network
    {
        public static Network Main => GetNetwork("Main") ?? Register(new BitcoinMain());

        public static Network TestNet => GetNetwork("TestNet") ?? Register(new BitcoinTest());

        public static Network RegTest => GetNetwork("RegTest") ?? Register(new BitcoinRegTest());

        public static Network StratisMain => GetNetwork("StratisMain") ?? Register(new StratisMain());

        public static Network StratisTest => GetNetwork("StratisTest") ?? Register(new StratisTest());

        public static Network StratisRegTest => GetNetwork("StratisRegTest") ?? Register(new StratisRegTest());
    }
}
