using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// The federation nodes take turns to be the Boss.  The boss gets to be first to build and 
    /// broadcast a federation multi-sig transaction.  The hash of the source transaction is
    /// combined with each node's public key and the combined string is hashed. Those hashes are
    /// sorted.  This gives us the BossTable - a table of hashes that each node can reproduce.
    /// The Boss is the first federation node on the list. The boss gets a set amount of time
    /// (5 mins) to build and broadcast the transaction. If he does not do his job, then the next
    /// guy becomes the boss.
    /// </summary>
    internal sealed class BossTableBuilder
    {
        /// <summary>
        /// Builds the boss table. The boss table contains an entry for each participating member
        /// node on the network. 
        /// </summary>
        /// <param name="sessionId">The hash of the source transaction is used as the sessionId.</param>
        /// <param name="keys">An array of the public keys of each federation member.</param>
        /// <returns></returns>
        public BossTable Build(uint256 sessionId, IEnumerable<string> keys)
        {
            // BossTableEntries are strings.
            // Hash the concatination of the sessionId and the key to get each entry in the table.
            var bossCards = keys
                .Select(key => BossTable.MakeBossTableEntry(sessionId, key).ToString())
                .OrderBy(k => k);

            // We now have a table that is reproducable on every federation node on the network.
            return new BossTable(bossCards.ToList());
        }
    }

    /// <summary>
    /// The BossTable.
    /// </summary>
    internal sealed class BossTable
    {
        /// <summary>
        /// Each entry in the BossTable.
        /// </summary>
        public List<string> BossTableEntries { get; }

        public BossTable(List<string> bossTableEntries)
        {
            this.BossTableEntries = bossTableEntries;
        }

        // The interval before the boss card changes hands.
        private readonly int bossCardHoldTime = (int) new TimeSpan(hours: 0, minutes: 1, seconds: 0).TotalSeconds;

        /// <summary>
        /// Hashes the SessionId with the key.
        /// </summary>
        /// <param name="sessionId">The transaction's sessionId.</param>
        /// <param name="key"></param>
        /// <returns>The hash of the concatenation of both pieces of data.</returns>
        public static uint256 MakeBossTableEntry(uint256 sessionId, string key)
        {
            var mergedBytes = Encoding.UTF8.GetBytes($"{sessionId}{key}");
            return NBitcoin.Crypto.Hashes.Hash256(mergedBytes);
        }

        /// <summary>
        /// Given the elapsed time we can return who holds the bossCard.
        /// </summary>
        /// <param name="startTime">Time when the session started.</param>
        /// <param name="now">Time now.</param>
        /// <returns>The BossTableEntry that holds the boss card or null if we have moved into free for all mode.</returns>
        public string WhoHoldsTheBossCard(DateTime startTime, DateTime now)
        {
            if (now < startTime)
                throw new ArgumentOutOfRangeException("The now time must be greater than the start time.");

            var secondsPassed = (int)(now - startTime).TotalSeconds;
            var cardPasses = secondsPassed / this.bossCardHoldTime;

            // If each federation has had a turn we are in free for all mode and anyone can build and broadcast.
            if (cardPasses >= BossTableEntries.Count) return null;

            return this.BossTableEntries[cardPasses];
        }
    }
}
