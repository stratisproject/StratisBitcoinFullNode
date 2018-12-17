using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using NSubstitute;

using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;

using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class MaturedBlockReceiverTests
    {
        [Fact]
        public void PushMaturedBlockDeposits_Should_Log_Good_Messages()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var logger = Substitute.For<ILogger>();
            loggerFactory.CreateLogger(null).ReturnsForAnyArgs(logger);

            var emptyBlock = Utils.TestingValues.GetMaturedBlockDeposits(0);
            var nonEmptyBlock = Utils.TestingValues.GetMaturedBlockDeposits(2);

            var maturedBlockDeposits = new[]
               {
                   emptyBlock,
                   nonEmptyBlock
               };

            var maturedBlockReceiver = new MaturedBlockReceiver(loggerFactory);

            maturedBlockReceiver.PushMaturedBlockDeposits(maturedBlockDeposits);

            var expectedLog =
                "block: 1119508747-43d6818610d880f517afb4b29d4a9b493796b49fadb4ee1269d68f16f5670f1f - deposits: \r\nblock: 1967246960-1891e2c6b795004f606a580bdf8dc709b5bf575285628aeb218fa13459b14e6a - deposits: [(Id) f9085ad70b330950e312ff6448ba7d5a1828a3924c2ac67cbbfa14dde46534a1 | (TargetAddress) qcysjps73pbxqm3cg2g36xdaoliw1p | (Amount) 942221385.00000000],[(Id) d1573bd7000d077922a3ee399ed64c3da2817040b507528abf06f27e0331be21 | (TargetAddress) ow2q8h443e95bhxhvddqkwiz78m9vj | (Amount) 372011428.00000000]";

            var logCalls = logger.ReceivedCalls();

            logCalls.Select(c => c.GetOriginalArguments()[0]).Should().AllBeEquivalentTo(LogLevel.Debug);

            var receivedMessages = logCalls.Select(c => c.GetOriginalArguments()[2].ToString()).ToList();
            receivedMessages.Should().BeEquivalentTo(new[] { "Pushing 2 matured deposit(s)", JsonConvert.SerializeObject(maturedBlockDeposits) });
        }
    }
}
