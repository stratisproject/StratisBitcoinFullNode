using System;
using System.Collections.Generic;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    /// <summary>
    /// Tests of <see cref="InvalidBlockHashStoreTest"/> class.
    /// </summary>
    public class InvalidBlockHashStoreTest
    {
        /// <summary>Source of randomness.</summary>
        private static Random rng = new Random();

        /// <summary>
        /// Tests <see cref="InvalidBlockHashStore.MarkInvalid(uint256, DateTime?)"/> and <see cref="InvalidBlockHashStore.IsInvalid(uint256)"/>
        /// for both permantently and temporary bans of blocks.
        /// </summary>
        [Fact]
        public void MarkInvalid_PermanentAndTemporaryBans()
        {
            var invalidBlockHashStore = new InvalidBlockHashStore(DateTimeProvider.Default);

            // Create some hashes that will be banned forever.
            var hashesBannedPermanently = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000001"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000002"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000003"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000004"),
            };

            foreach (uint256 hash in hashesBannedPermanently)
                invalidBlockHashStore.MarkInvalid(hash);

            // Create some hashes that will be banned now, but not in 5 seconds.
            var hashesBannedTemporarily1 = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000011"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000012"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000013"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000014"),
            };

            foreach (uint256 hash in hashesBannedTemporarily1)
                invalidBlockHashStore.MarkInvalid(hash, DateTime.UtcNow.AddMilliseconds(rng.Next(2000, 5000)));

            // Create some hashes that will be banned now and also after 5 seconds.
            var hashesBannedTemporarily2 = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000021"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000022"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000023"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000024"),
            };

            foreach (uint256 hash in hashesBannedTemporarily2)
                invalidBlockHashStore.MarkInvalid(hash, DateTime.UtcNow.AddMilliseconds(rng.Next(20000, 50000)));

            // Check that all hashes we have generated are banned now.
            var allHashes = new List<uint256>(hashesBannedPermanently);
            allHashes.AddRange(hashesBannedTemporarily1);
            allHashes.AddRange(hashesBannedTemporarily2);

            foreach (uint256 hash in allHashes)
                Assert.True(invalidBlockHashStore.IsInvalid(hash));

            // Wait 5 seconds and then check if hashes from first temporary group are no longer banned and all others still are.
            Thread.Sleep(5000);

            foreach (uint256 hash in allHashes)
            {
                uint num = hash.GetLow32();
                bool isSecondGroup = (0x10 <= num) && (num < 0x20);
                Assert.Equal(!isSecondGroup, invalidBlockHashStore.IsInvalid(hash));
            }
        }

        /// <summary>
        /// Tests that the hash store behaves correctly when its capacity is reached.
        /// Oldest entries should be removed when capacity is reached.
        /// </summary>
        [Fact]
        public void ReachingStoreCapacity_RemovesOldestEntries()
        {
            var invalidBlockHashStore = new InvalidBlockHashStore(DateTimeProvider.Default, 10);

            // Create some hashes that will be banned forever.
            var hashesBannedPermanently = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000001"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000002"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000003"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000004"),
            };

            foreach (uint256 hash in hashesBannedPermanently)
                invalidBlockHashStore.MarkInvalid(hash);

            // Create some hashes that will be banned.
            var hashesBannedTemporarily1 = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000011"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000012"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000013"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000014"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000015"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000016"),
            };

            foreach (uint256 hash in hashesBannedTemporarily1)
                invalidBlockHashStore.MarkInvalid(hash, DateTime.UtcNow.AddMilliseconds(rng.Next(2000, 5000)));

            // Check that all hashes we have generated are banned now.
            var allHashes = new List<uint256>(hashesBannedPermanently);
            allHashes.AddRange(hashesBannedTemporarily1);

            foreach (uint256 hash in allHashes)
                Assert.True(invalidBlockHashStore.IsInvalid(hash));

            // Add two more hashes that will be banned.
            var hashesBannedTemporarily2 = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000031"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000032"),
            };

            foreach (uint256 hash in hashesBannedTemporarily2)
                invalidBlockHashStore.MarkInvalid(hash, DateTime.UtcNow.AddMilliseconds(rng.Next(20000, 50000)));

            // As the capacity is just 10, the first two hashes should no longer be banned at this point.
            Assert.False(invalidBlockHashStore.IsInvalid(hashesBannedPermanently[0]));
            Assert.False(invalidBlockHashStore.IsInvalid(hashesBannedPermanently[1]));

            // And all other hashes are still banned.
            var allBannedHashes = new List<uint256>();
            allBannedHashes.Add(hashesBannedPermanently[2]);
            allBannedHashes.Add(hashesBannedPermanently[3]);
            allBannedHashes.AddRange(hashesBannedTemporarily1);
            allBannedHashes.AddRange(hashesBannedTemporarily2);

            foreach (uint256 hash in allBannedHashes)
                Assert.True(invalidBlockHashStore.IsInvalid(hash));
        }

        /// <summary>
        /// Another test of behavior of the hash store when its capacity is reached.
        /// The internal implementation of the block hash store works with a dictionary and a circular array,
        /// which should skip the entries removed from the dictionary.
        /// </summary>
        [Fact]
        public void ReachingStoreCapacity_CircularArraySkipsExpiredEntries()
        {
            var invalidBlockHashStore = new InvalidBlockHashStore(DateTimeProvider.Default, 10);

            // Create some hashes that will be banned forever.
            var hashesBannedPermanently1 = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000001"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000002"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000003"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000004"),
            };

            foreach (uint256 hash in hashesBannedPermanently1)
                invalidBlockHashStore.MarkInvalid(hash);

            // Create some hashes that will be banned temporarily, but not after 5 seconds.
            var hashesBannedTemporarily = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000011"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000012"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000013"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000014"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000015"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000016"),
            };

            foreach (uint256 hash in hashesBannedTemporarily)
                invalidBlockHashStore.MarkInvalid(hash, DateTime.UtcNow.AddMilliseconds(rng.Next(2000, 5000)));

            // Add more forever bans.
            var hashesBannedPermanently2 = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000021"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000022"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000023"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000024"),
            };

            foreach (uint256 hash in hashesBannedPermanently2)
                invalidBlockHashStore.MarkInvalid(hash);

            // Now check that all hashes from the first group are no longer banned and all others still are.
            var allBannedHashes = new List<uint256>(hashesBannedTemporarily);
            allBannedHashes.AddRange(hashesBannedPermanently2);

            foreach (uint256 hash in hashesBannedPermanently1)
                Assert.False(invalidBlockHashStore.IsInvalid(hash));

            foreach (uint256 hash in allBannedHashes)
                Assert.True(invalidBlockHashStore.IsInvalid(hash));

            // Now we wait 5 seconds and touch the first four temporarily banned hashes,
            // which should remove them from the dictionary.
            Thread.Sleep(5000);

            Assert.False(invalidBlockHashStore.IsInvalid(hashesBannedTemporarily[0]));
            Assert.False(invalidBlockHashStore.IsInvalid(hashesBannedTemporarily[1]));
            Assert.False(invalidBlockHashStore.IsInvalid(hashesBannedTemporarily[2]));
            Assert.False(invalidBlockHashStore.IsInvalid(hashesBannedTemporarily[3]));

            // Then we add a new hash, which should remove the four hashes from the circular array as well.
            invalidBlockHashStore.MarkInvalid(uint256.Parse("0000000000000000000000000000000000000000000000000000000000000031"));

            Assert.Equal(6, invalidBlockHashStore.orderedHashList.Count);
        }

        /// <summary>
        /// Checks that the hash store behaves correctly when all its entries expired and were removed.
        /// </summary>
        [Fact]
        public void AllEntriesExpired()
        {
            var invalidBlockHashStore = new InvalidBlockHashStore(DateTimeProvider.Default, 6);

            // Create some hashes that will be banned temporarily, but not after 5 seconds.
            var hashesBannedTemporarily = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000011"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000012"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000013"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000014"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000015"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000016"),
            };

            foreach (uint256 hash in hashesBannedTemporarily)
                invalidBlockHashStore.MarkInvalid(hash, DateTime.UtcNow.AddMilliseconds(rng.Next(2000, 5000)));

            // Check all hashes are banned now.
            foreach (uint256 hash in hashesBannedTemporarily)
                Assert.True(invalidBlockHashStore.IsInvalid(hash));

            // Wait 5 seconds and touch all the entries, so they are removed from the store's dictionary.
            Thread.Sleep(5000);

            // Check all hashes are no longer banned.
            foreach (uint256 hash in hashesBannedTemporarily)
                Assert.False(invalidBlockHashStore.IsInvalid(hash));

            // Add a new hash, which should remove all the other entries from the store's circular array as well.
            uint256 lastHash = uint256.Parse("0000000000000000000000000000000000000000000000000000000000000031");
            invalidBlockHashStore.MarkInvalid(lastHash);

            // Check all removed hashes are no longer banned.
            foreach (uint256 hash in hashesBannedTemporarily)
                Assert.False(invalidBlockHashStore.IsInvalid(hash));

            // Check the number of entries is now 1.
            Assert.Equal(1, invalidBlockHashStore.orderedHashList.Count);

            // Check the last entry is banned.
            Assert.True(invalidBlockHashStore.IsInvalid(lastHash));
        }
    }
}
