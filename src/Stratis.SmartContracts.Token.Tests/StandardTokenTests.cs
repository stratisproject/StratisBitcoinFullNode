using System;
using Moq;
using Stratis.SmartContracts.Core;
using Xunit;

namespace Stratis.SmartContracts.Token.Tests
{
    public class StandardTokenTests
    {
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;

        public StandardTokenTests()
        {
            this.mockContractLogger = new Mock<IContractLogger>();
            this.mockPersistentState = new Mock<IPersistentState>();
            this.mockContractState = new Mock<ISmartContractState>();
            this.mockContractState.Setup(s => s.PersistentState).Returns(this.mockPersistentState.Object);
            this.mockContractState.Setup(s => s.ContractLogger).Returns(this.mockContractLogger.Object);
        }

        [Fact]
        public void Constructor_Sets_TotalSupply()
        {
            uint totalSupply = 100_000;
            var standardToken = new StandardToken(this.mockContractState.Object, totalSupply);

            // Verify that PersistentState was called with the total supply
            this.mockPersistentState.Verify(s => s.SetUInt32(nameof(StandardToken.TotalSupply), totalSupply));
        }

        [Fact]
        public void GetBalance_Returns_Correct_Balance()
        {
            uint balance = 100;
            Address address = new Address("test address 1");
            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{address}")).Returns(balance);

            Assert.Equal(balance, standardToken.GetBalance(address));
        }

        [Fact]
        public void Approve_Sets_Approval_Correctly()
        {
            uint balance = 100_000;
            uint approval = 1000;

            Address owner = new Address("owner");
            Address spender = new Address("spender");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), owner, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            standardToken.Approve(spender, approval);

            this.mockPersistentState.Verify(s => s.SetUInt32($"Allowance:{owner}:{spender}", approval));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.ApprovalLog { Owner = owner, Spender = spender, Amount = approval }));
        }

        [Fact]
        public void Allowance_Gets_Correctly()
        {
            uint balance = 100_000;
            uint approval = 1000;

            Address owner = new Address("owner");
            Address spender = new Address("spender");

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            standardToken.Allowance(owner, spender);

            this.mockPersistentState.Verify(s => s.GetUInt32($"Allowance:{owner}:{spender}"));
        }

        [Fact]
        public void Transfer_0_Returns_True()
        {
            uint amount = 0;
            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            Assert.True(standardToken.Transfer(destination, amount));
            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = sender, To = destination, Amount = amount }));
        }

        [Fact]
        public void Transfer_Greater_Than_Balance_Returns_False()
        {
            uint balance = 0;
            uint amount = balance + 1;
            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{sender}")).Returns(balance);

            Assert.False(standardToken.Transfer(destination, amount));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{sender}"));
        }

        [Fact]
        public void Transfer_To_Destination_With_Balance_Greater_Than_uint_MaxValue_Returns_False()
        {
            uint destinationBalance = uint.MaxValue;
            uint senderBalance = 100;
            uint amount = senderBalance - 1; // Transfer less than the balance

            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{sender}")).Returns(senderBalance);

            // Setup the destination's balance to be uint.MaxValue
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{destination}")).Returns(destinationBalance);

            Assert.ThrowsAny<OverflowException>(() => standardToken.Transfer(destination, amount));

            // Verify we queried the sender's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{sender}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{destination}"));
        }

        [Fact]
        public void Transfer_To_Destination_Success_Returns_True()
        {
            uint destinationBalance = 400_000;
            uint senderBalance = 100;
            uint amount = senderBalance - 1; // Transfer less than the balance

            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            int callOrder = 1;

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{sender}")).Returns(senderBalance)
                .Callback(() => Assert.Equal(1, callOrder++));

            // Setup the destination's balance
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{destination}")).Returns(destinationBalance)
                .Callback(() => Assert.Equal(2, callOrder++));

            // Setup the sender's balance
            this.mockPersistentState.Setup(s => s.SetUInt32($"Balance:{sender}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(3, callOrder++));

            // Setup the destination's balance. Important that this happens AFTER setting the sender's balance
            this.mockPersistentState.Setup(s => s.SetUInt32($"Balance:{destination}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(4, callOrder++));

            Assert.True(standardToken.Transfer(destination, amount));

            // Verify we queried the sender's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{sender}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{destination}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt32($"Balance:{sender}", senderBalance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt32($"Balance:{destination}", destinationBalance + amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = sender, To = destination, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_0_Returns_True()
        {
            uint amount = 0;
            Address owner = new Address("owner");
            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            Assert.True(standardToken.TransferFrom(owner, destination, amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = owner, To = destination, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_Greater_Than_Senders_Allowance_Returns_False()
        {
            uint allowance = 0;
            uint amount = allowance + 1;
            var balance = amount + 1; // Balance should be more than amount we are trying to send

            Address owner = new Address("owner");
            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{owner}")).Returns(balance);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Allowance:{owner}:{sender}")).Returns(allowance);

            Assert.False(standardToken.TransferFrom(owner, destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Allowance:{owner}:{sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{owner}"));
        }

        [Fact]
        public void TransferFrom_Greater_Than_Owners_Balance_Returns_False()
        {
            uint balance = 0; // Balance should be less than amount we are trying to send
            uint amount = balance + 1;
            uint allowance = amount + 1; // Allowance should be more than amount we are trying to send
            Address owner = new Address("owner");
            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{owner}")).Returns(balance);

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Allowance:{owner}:{sender}")).Returns(allowance);

            Assert.False(standardToken.TransferFrom(owner, destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Allowance:{owner}:{sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{owner}"));
        }

        [Fact]
        public void TransferFrom_To_Destination_With_Balance_Greater_Than_uint_MaxValue_Returns_False()
        {
            uint destinationBalance = uint.MaxValue; // Destination balance should be uint.MaxValue
            uint amount = 1;
            uint allowance = amount + 1; // Allowance should be more than amount we are trying to send
            uint ownerBalance = allowance + 1; // Owner balance should be more than allowance

            Address owner = new Address("owner");
            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{owner}")).Returns(ownerBalance);

            // Setup the balance of the destination in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{destination}")).Returns(destinationBalance);

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Allowance:{owner}:{sender}")).Returns(allowance);

            Assert.ThrowsAny<OverflowException>(() => standardToken.TransferFrom(owner, destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Allowance:{owner}:{sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{owner}"));
        }

        [Fact]
        public void TransferFrom_To_Destination_Success_Returns_True()
        {
            uint destinationBalance = 100; // Destination balance should be uint.MaxValue
            uint amount = 1;
            uint allowance = amount + 1; // Allowance should be more than amount we are trying to send
            uint ownerBalance = allowance + 1; // Owner balance should be more than allowance

            Address owner = new Address("owner");
            Address sender = new Address("sender");
            Address destination = new Address("destination");

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(new Address("contract"), sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000);

            int callOrder = 1;

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Allowance:{owner}:{sender}")).Returns(allowance)
                .Callback(() => Assert.Equal(1, callOrder++));

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{owner}")).Returns(ownerBalance)
                .Callback(() => Assert.Equal(2, callOrder++));

            // Setup the balance of the destination in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32($"Balance:{destination}")).Returns(destinationBalance)
                .Callback(() => Assert.Equal(3, callOrder++));

            // Set the sender's new allowance
            this.mockPersistentState.Setup(s => s.SetUInt32($"Allowance:{owner}:{sender}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(4, callOrder++));

            // Set the owner's new balance
            this.mockPersistentState.Setup(s => s.SetUInt32($"Balance:{owner}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(5, callOrder++));

            // Setup the destination's balance. Important that this happens AFTER setting the owner's balance
            this.mockPersistentState.Setup(s => s.SetUInt32($"Balance:{destination}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(6, callOrder++));

            Assert.True(standardToken.TransferFrom(owner, destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Allowance:{owner}:{sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{owner}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt32($"Balance:{destination}"));

            // Verify we set the sender's allowance
            this.mockPersistentState.Verify(s => s.SetUInt32($"Allowance:{owner}:{sender}", allowance - amount));

            // Verify we set the owner's balance
            this.mockPersistentState.Verify(s => s.SetUInt32($"Balance:{owner}", ownerBalance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt32($"Balance:{destination}", destinationBalance + amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = owner, To = destination, Amount = amount }));
        }
    }
}
