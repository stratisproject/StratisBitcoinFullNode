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
        /// <param name="blockHeight">The height of the block containing the crosschain transaction</param>
        /// <param name="keys">An array of the public keys of each federation member.</param>
        /// <returns></returns>
        public BossTable Build(int blockHeight, IEnumerable<string> keys)
        {
            var bossCards = keys
                .Select(key => BossTable.MakeBossTableEntry(blockHeight, key).ToString())
                .OrderBy(k => k);
            
            return new BossTable(bossCards.ToList());
        }
    }

    public sealed class BossTable
    {
        public List<string> BossTableEntries { get; }

        public BossTable(List<string> bossTableEntries)
        {
            this.BossTableEntries = bossTableEntries;
        }

        // The interval before the boss card changes hands.
        private readonly int bossCardHoldTime = (int) new TimeSpan(hours: 0, minutes: 1, seconds: 0).TotalSeconds;

        public static uint256 MakeBossTableEntry(int blockHeight, string key)
        {
            var mergedBytes = Encoding.UTF8.GetBytes($"{blockHeight}{key}");
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
