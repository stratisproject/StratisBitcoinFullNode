using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Internal;
using Stratis.FederatedPeg.Features.FederationGateway;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class TransferStatusTests
    {
        [Fact]
        public void ToString_Should_Return_The_Name_Of_The_Instantiated_TransferStatus()
        {
            var status = TransferStatus.PartialTransactionRejected;
            status.ToString().Should().Be("PartialTransactionRejected");
        }

        [Fact]
        public void GetAll_Should_Return_All_The_Instantiated_TransferStatus()
        {
            var statuses = TransferStatus.GetAll();
            statuses.Count.Should().Be(10);
        }

        [Fact]
        public void GetAllAsString_Should_Return_All_The_Instantiated_TransferStatus()
        {
            var statuses = TransferStatus.GetAllAsString();
            statuses.Distinct().Count().Should().Be(10);
        }
    }
}
