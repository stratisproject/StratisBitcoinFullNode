using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Sidechains.Networks;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Receipts
{
    public class ReceiptMatcherTests
    {
        private readonly Network network;

        public ReceiptMatcherTests()
        {
            this.network = new CirrusRegTest();
        }

        [Fact]
        public void MatchReceipts_Success()
        {
            var receipts = new List<Receipt>();

            for (var i = 0; i < 10; i++)
            {
                var logs = new List<Log>();

                // Generate some logs.
                for (var j = 0; j < 10; j++)
                {
                    logs.Add(
                        new Log(
                            new uint160((ulong) j),
                            new List<byte[]>
                            {
                                Encoding.UTF8.GetBytes("Topic" + j),
                                Encoding.UTF8.GetBytes("Event" + i),
                            },
                            null
                        ));
                }

                receipts.Add(
                    new Receipt(new uint256((ulong)i), 0, logs.ToArray())
                );
            }

            var matcher = new ReceiptMatcher();
            
            // Search for the "Event" + i receipts.
            // Every receipt should have at least one log like this.
            for (int i = 0; i < 10; i++)
            {
                List<Receipt> matches = matcher.MatchReceipts(receipts, null, 
                    new List<byte[]>
                    {
                        Encoding.UTF8.GetBytes("Event" + i)
                    });

                Assert.Single(matches);

                matches = matcher.MatchReceipts(receipts, null,
                    new List<byte[]>
                    {
                        Encoding.UTF8.GetBytes("Topic" + i)
                    });

                Assert.Equal(10, matches.Count);

                matches = matcher.MatchReceipts(receipts, new uint160((ulong)i), null);

                Assert.Equal(10, matches.Count);
            }
        }
    }
}
