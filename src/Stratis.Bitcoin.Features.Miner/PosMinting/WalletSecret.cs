namespace Stratis.Bitcoin.Features.Miner.PosMinting
{
    public partial class PosMinting
    {
        public partial class WalletSecret
        {
            /// <summary>Wallet's password that is needed for getting wallet's private key which is used for signing generated blocks.</summary>
            public string WalletPassword { get; set; }

            /// <summary>Name of the wallet which UTXOs are used for staking.</summary>
            public string WalletName { get; set; }
        }
    }
}