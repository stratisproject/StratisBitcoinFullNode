using NBitcoin;

namespace Obsidian.Coin
{
    public class ObsidianTestConsensusOptions : ObsidianMainConsensusOptions
    {
	    public ObsidianTestConsensusOptions()
	    {
		    COINBASE_MATURITY = 10;
		    StakeMinConfirmations = 10;
	    }
	}
}
