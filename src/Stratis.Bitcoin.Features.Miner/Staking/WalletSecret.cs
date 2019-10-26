namespace Stratis.Bitcoin.Features.Miner.Staking
{
    /// <summary>
    /// Credentials to wallet that contains the private key for the staking UTXO.
    /// </summary>
    public class WalletSecret
    {
        /// <summary>Wallet's password that is needed for getting wallet's private key which is used for signing generated blocks.</summary>
        public string WalletPassword { get; set; }

        /// <summary>Name of the wallet which UTXOs are used for staking.</summary>
        public string WalletName { get; set; }
    }
}