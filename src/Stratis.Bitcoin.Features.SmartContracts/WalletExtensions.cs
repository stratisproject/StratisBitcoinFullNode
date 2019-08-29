using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public static class WalletExtensions
    {
        private const int MinConfirmationsAllChecks = 0;

        public static List<OutPoint> GetSpendableInputsForAddress(this IWalletManager walletManager, string walletName, string address, int minConfirmations = MinConfirmationsAllChecks)
        {
            return walletManager.GetSpendableTransactionsInWallet(walletName, minConfirmations).Where(x => x.Address.Address == address).Select(x => x.ToOutPoint()).ToList();
        }
    }
}
