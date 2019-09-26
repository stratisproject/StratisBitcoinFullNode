using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using SQLite;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository.Extensions;

namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class HDWallet
    {
        public string Name { get; set; }
        [PrimaryKey]
        public int WalletId { get; set; }
        public int LastBlockSyncedHeight { get; set; }
        public string LastBlockSyncedHash { get; set; }
        public bool IsExtPubKeyWallet { get; set; }
        public string EncryptedSeed { get; set; }
        public string ChainCode { get; set; }
        public string BlockLocator { get; set; }
        public long CreationTime { get; set; }

        internal uint256[] GetBlockLocatorHashes()
        {
            if (string.IsNullOrEmpty(this.BlockLocator.Trim()))
                return new uint256[] { };

            return this.BlockLocator.Split(',').Select(strHash => uint256.Parse(strHash)).ToArray();
        }

        internal static IEnumerable<string> CreateScript()
        {
            yield return $@"
            CREATE TABLE HDWallet (
                Name                TEXT NOT NULL,
                WalletId            INTEGER PRIMARY KEY AUTOINCREMENT,
                LastBlockSyncedHeight INTEGER NOT NULL,
                LastBlockSyncedHash TEXT NOT NULL,
                IsExtPubKeyWallet   INTEGER NOT NULL,
                EncryptedSeed       TEXT,
                ChainCode           TEXT,
                BlockLocator        TEXT NOT NULL,
                CreationTime        INTEGER NOT NULL
            );";

            yield return "CREATE UNIQUE INDEX UX_HDWallet_Name ON HDWallet(Name)";
        }

        internal static void CreateTable(DBConnection conn)
        {
            foreach (string command in CreateScript())
                conn.Execute(command);
        }

        internal void CreateWallet(SQLiteConnection conn)
        {
            conn.Execute($@"
            REPLACE INTO HDWallet (
                    Name
            ,       LastBlockSyncedHeight
            ,       LastBlockSyncedHash
            ,       IsExtPubKeyWallet
            ,       EncryptedSeed
            ,       ChainCode
            ,       BlockLocator
            ,       CreationTime
            )
            VALUES ('{this.Name}'
            ,       {this.LastBlockSyncedHeight}
            ,       '{this.LastBlockSyncedHash}'
            ,       {this.IsExtPubKeyWallet}
            ,       '{this.EncryptedSeed}'
            ,       '{this.ChainCode}'
            ,       '{this.BlockLocator}'
            ,       {this.CreationTime})");

            this.WalletId = conn.ExecuteScalar<int>($@"
            SELECT  WalletId
            FROM    HDWallet
            WHERE   Name = '{this.Name}'");
        }

        internal static HDWallet GetByName(SQLiteConnection conn, string walletName)
        {
            return conn.FindWithQuery<HDWallet>($@"
                SELECT *
                FROM   HDWallet
                WHERE  Name = ?", walletName);
        }

        internal static IEnumerable<HDWallet> GetAll(SQLiteConnection conn)
        {
            return conn.Query<HDWallet>($@"
                SELECT *
                FROM   HDWallet");
        }

        internal static HDWallet GetWalletByEncryptedSeed(SQLiteConnection conn, string encryptedSeed)
        {
            return conn.FindWithQuery<HDWallet>($@"
                SELECT *
                FROM   HDWallet
                WHERE  EncryptedSeed = ?", encryptedSeed);
        }

        internal class HeightHashPair
        {
            internal int Height { get; set; }
            internal string Hash { get; set; }
        }

        /*
        internal static HeightHashPair GreatestBlockHeightBeforeOrAt(SQLiteConnection conn, int walletId, int height)
        {
            return conn.FindWithQuery<HeightHashPair>($@"
                SELECT  OutputBlockHeight BlockHeight
                ,       OutputBlockHash BlockHash
                FROM    HDTransactionData
                WHERE   WalletId = { walletId }
                AND     OutputBlockHeight <= { height }
                UNION   ALL
                SELECT  SpendBlockHeight BlockHeight
                ,       SpendBlockHash BlockHash
                FROM    HDTransactionData
                WHERE   WalletId = { walletId }
                AND     SpendBlockHeight <= { height }
                ORDER   BY BlockHeight desc
                LIMIT   1");
        }
        */

        internal static void AdvanceTip(SQLiteConnection conn, HDWallet wallet, ChainedHeader newTip, uint256 prevTipHash)
        {
            uint256 lastBlockSyncedHash = newTip?.HashBlock ?? uint256.Zero;
            int lastBlockSyncedHeight = newTip?.Height ?? -1;
            string blockLocator = "";
            if (newTip != null)
                blockLocator = string.Join(",", newTip?.GetLocator().Blocks);

            conn.Execute($@"
                    UPDATE HDWallet
                    SET    LastBlockSyncedHash = '{lastBlockSyncedHash}',
                           LastBlockSyncedHeight = {lastBlockSyncedHeight},
                           BlockLocator = '{blockLocator}'
                    WHERE  LastBlockSyncedHash = '{prevTipHash}' {
                    // Respect the wallet name if provided.
                    ((wallet?.Name != null) ? $@"
                    AND    Name = '{wallet?.Name}'" : "")}");
        }

        internal void SetLastBlockSynced(HashHeightPair lastBlockSynced, BlockLocator blockLocator, Network network)
        {
            uint256 lastBlockSyncedHash = lastBlockSynced?.Hash ?? (uint256)0;

            Guard.Assert(((blockLocator?.Blocks?.Count ?? 0) == 0 && lastBlockSyncedHash == 0) || (blockLocator.Blocks[0] == lastBlockSyncedHash));

            this.LastBlockSyncedHash = lastBlockSyncedHash.ToString();
            this.LastBlockSyncedHeight = lastBlockSynced?.Height ?? -1;
            this.BlockLocator = (blockLocator == null) ? "" : string.Join(",", blockLocator.Blocks);
        }

        internal void SetLastBlockSynced(ChainedHeader lastBlockSynced, Network network)
        {
            SetLastBlockSynced((lastBlockSynced == null) ? null : new HashHeightPair(lastBlockSynced), lastBlockSynced?.GetLocator(), network);
        }

        internal ChainedHeader GetFork(ChainedHeader chainTip)
        {
            if (chainTip == null)
                return null;

            if (chainTip.Height > this.LastBlockSyncedHeight)
            {
                if (this.LastBlockSyncedHeight < 0)
                    return null;

                chainTip = chainTip.GetAncestor(this.LastBlockSyncedHeight);
            }

            if (chainTip.Height == this.LastBlockSyncedHeight)
            {
                if (chainTip.HashBlock == uint256.Parse(this.LastBlockSyncedHash))
                    return chainTip;
                else
                    return null;
            }

            var blockLocator = new BlockLocator()
            {
                Blocks = this.BlockLocator.Split(',').Select(strHash => uint256.Parse(strHash)).ToList()
            };

            List<int> locatorHeights = ChainedHeaderExt.GetLocatorHeights(this.LastBlockSyncedHeight);

            for (int i = 0; i < locatorHeights.Count; i++)
            {
                if (chainTip.Height > locatorHeights[i])
                    chainTip = chainTip.GetAncestor(locatorHeights[i]);

                if (chainTip.HashBlock == blockLocator.Blocks[i])
                    return chainTip;
            }

            return null;
        }

        internal bool WalletContainsBlock(ChainedHeader lastBlockSynced)
        {
            if (lastBlockSynced == null)
                return true;

            if (this.LastBlockSyncedHeight <= lastBlockSynced.Height)
                return uint256.Parse(this.LastBlockSyncedHash) == lastBlockSynced.HashBlock;

            uint256[] hashes = GetBlockLocatorHashes();
            if (hashes.Length == 0)
                return false;

            int[] heights = ChainedHeaderExt.GetLocatorHeights(this.LastBlockSyncedHeight).ToArray();

            return hashes.Select((h, n) => lastBlockSynced.HashBlock == h && lastBlockSynced.Height == heights[n]).Any(e => true);
        }
    }
}
