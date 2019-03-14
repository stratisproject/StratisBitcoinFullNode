﻿using System.Threading.Tasks;

namespace Stratis.Bitcoin.NBitcoin.OpenAsset
{
    public static class Extensions
    {
        public static ColoredTransaction GetColoredTransaction(this Transaction tx, IColoredTransactionRepository repo)
        {
            return tx.GetColoredTransactionAsync(repo).GetAwaiter().GetResult();
        }

        public static async Task<ColoredTransaction> GetColoredTransactionAsync(this Transaction tx, IColoredTransactionRepository repo)
        {
            try
            {
                return await ColoredTransaction.FetchColorsAsync(tx, repo).ConfigureAwait(false);
            }
            catch(TransactionNotFoundException)
            {
                return null;
            }
        }

        public static ColorMarker GetColoredMarker(this Transaction tx)
        {
            return ColorMarker.Get(tx);
        }
        public static bool HasValidColoredMarker(this Transaction tx)
        {
            return ColorMarker.HasValidColorMarker(tx);
        }
        public static AssetId ToAssetId(this ScriptId id)
        {
            return new AssetId(id);
        }
    }
}
