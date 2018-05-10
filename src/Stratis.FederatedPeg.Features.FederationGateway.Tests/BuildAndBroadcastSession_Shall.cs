using System;
using System.IO;
using FluentAssertions;
using Moq;
using NBitcoin;
using Xunit;

namespace Stratis.FederatedPeg.Features.FederationGateway.Tests
{
    public class BuildAndBroadcastSession_Shall
    {
        //[Fact] public void create_a_sorted_boss_table()
        //{
        //    //create a random transaction
        //    string transactionHex = "010000000001010000000000000000000000000000000000000000000000000000000000000000ffffffff230384041200fe0eb3a959fe1af507000963676d696e6572343208000000000000000000ffffffff02155e8b09000000001976a9144bfe90c8e6c6352c034b3f57d50a9a6e77a62a0788ac0000000000000000266a24aa21a9ed0bc6e4bfe82e04a1c52e66b72b199c5124794dd8c3c368f6ab95a0ba6cde277d0120000000000000000000000000000000000000000000000000000000000000000000000000";
        //    Transaction transaction = new Transaction(transactionHex);

        //    //create a federation
        //    var federation = new Mock<IFederation>();
        //    federation.Setup(m => m.GetPublicKeys(Chain.Mainchain)).Returns(new string[]{ "addr1", "addr2", "addr3" } );

        //    var memberFolderManager = new Mock<IMemberFolderManager>();
        //    memberFolderManager.Setup(m => m.LoadFederation(2,3)).Returns(federation.Object);

        //    var fundingTransactionInfo = new CrossChainTransactionInfo();
        //    fundingTransactionInfo.TransactionHash = transaction.GetHash();
        //    var buildBroadcastSession = new BuildAndBroadcastSession(Chain.Mainchain, DateTime.Now, memberFolderManager.Object, fundingTransactionInfo, "addr1");
        //    string[] bossTable = buildBroadcastSession.BuildBossTable();
        //    bossTable.Length.Should().Be(3);
        //    StringComparer.InvariantCulture.Compare(bossTable[0], bossTable[1]).Should().Be(-1);
        //    StringComparer.InvariantCulture.Compare(bossTable[1], bossTable[2]).Should().Be(-1);
        //}
    }
}
