namespace Obsidian.Coin
{
    public static class NetConfig
    {
		// Common
	    public static string UserAgentString => "Obsidian Stratis";
		public static ObsidianNet ObsidianNet { get; set; }

		// Mainnet
	    public static int MainnetPort => 56660;
	    public static int MainnetRpcPort => 56661;
	    public static bool UseSha512OnMain = false;

		// Testnet
		public static int TestnetPort => 56662;
	    public static int TestnetRpcPort => 56663;
	    public static bool UseSha512OnTest = false;
	}

	public enum ObsidianNet
	{
		NotSet = 0,
		Main = 1,
		Test = 2
	}
}
