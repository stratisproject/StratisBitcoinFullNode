using NBitcoin;

namespace City.Networks
{
    public static class CityNetworks
    {
        public static NetworksSelector City
        {
            get
            {
                return new NetworksSelector(() => new CityMain(), () => new CityTest(), () => new CityRegTest());
            }
        }
    }
}
