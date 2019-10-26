using System;
using System.Collections.Generic;
using System.Threading;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    /// <summary>
    /// Tests of <see cref="ChainState"/> class.
    /// </summary>
    public class ChainStateTest
    {
        /// <summary>Source of randomness.</summary>
        private static Random rng = new Random();

        /// <summary>
        /// Tests <see cref="ChainState.MarkBlockInvalid(uint256, DateTime?)"/> and <see cref="ChainState.IsMarkedInvalid(uint256)"/>
        /// for both permantently and temporary bans of blocks.
        /// </summary>
        /// <remarks>Note that this test is almost identical to <see cref="InvalidBlockHashStoreTest.MarkInvalid_PermanentAndTemporaryBans"/>
        /// as the <see cref="ChainState"/> is just a middleman between the users of the block hash store and the store itself.</remarks>
        [Fact]
        public void MarkBlockInvalid_PermanentAndTemporaryBans()
        {
            var fullNode = new Mock<IFullNode>();
            fullNode.Setup(f => f.NodeService<IDateTimeProvider>(true))
                .Returns(DateTimeProvider.Default);

            var store = new InvalidBlockHashStore(DateTimeProvider.Default);

            // Create some hashes that will be banned forever.
            var hashesBannedPermanently = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000001"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000002"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000003"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000004"),
            };

            foreach (uint256 hash in hashesBannedPermanently)
                store.MarkInvalid(hash);

            // Create some hashes that will be banned now, but not in 5 seconds.
            var hashesBannedTemporarily1 = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000011"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000012"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000013"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000014"),
            };

            foreach (uint256 hash in hashesBannedTemporarily1)
                store.MarkInvalid(hash, DateTime.UtcNow.AddMilliseconds(rng.Next(2000, 5000)));

            // Create some hashes that will be banned now and also after 5 seconds.
            var hashesBannedTemporarily2 = new uint256[]
            {
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000021"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000022"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000023"),
                uint256.Parse("0000000000000000000000000000000000000000000000000000000000000024"),
            };

            foreach (uint256 hash in hashesBannedTemporarily2)
                store.MarkInvalid(hash, DateTime.UtcNow.AddMilliseconds(rng.Next(20000, 50000)));

            // Check that all hashes we have generated are banned now.
            var allHashes = new List<uint256>(hashesBannedPermanently);
            allHashes.AddRange(hashesBannedTemporarily1);
            allHashes.AddRange(hashesBannedTemporarily2);

            foreach (uint256 hash in allHashes)
                Assert.True(store.IsInvalid(hash));

            // Wait 5 seconds and then check if hashes from first temporary group are no longer banned and all others still are.
            Thread.Sleep(5000);

            foreach (uint256 hash in allHashes)
            {
                uint num = hash.GetLow32();
                bool isSecondGroup = (0x10 <= num) && (num < 0x20);
                Assert.Equal(!isSecondGroup, store.IsInvalid(hash));
            }
        }
    }
}
