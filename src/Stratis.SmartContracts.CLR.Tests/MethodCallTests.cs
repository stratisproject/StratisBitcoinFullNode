using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class MethodCallTests
    {
        [Fact]
        public void MethodCall_Created_With_Factory_Should_Be_Receive()
        {
            var methodCall = MethodCall.Receive();

            Assert.True(methodCall.IsReceiveHandlerCall);
        }

        [Fact]
        public void MethodCall_Without_Params_Should_Be_Receive()
        {
            var methodCall = new MethodCall("");

            Assert.True(methodCall.IsReceiveHandlerCall);
        }

        [Fact]
        public void MethodCall_To_Receive_Should_Be_Receive()
        {
            var methodCall = new MethodCall("Receive");

            Assert.True(methodCall.IsReceiveHandlerCall);
        }

        [Fact]
        public void MethodCall_To_Receive_With_Params_Should_Not_Be_Receive()
        {
            var methodCall = new MethodCall("Receive", new object[] { 1 });

            Assert.False(methodCall.IsReceiveHandlerCall);
        }

        [Fact]
        public void MethodCall_With_Params_Should_Not_Be_Receive()
        {
            var methodCall = new MethodCall("", new object[] { 1 });

            Assert.False(methodCall.IsReceiveHandlerCall);
        }

        [Fact]
        public void MethodCall_Should_Return_Internal_Receive_Name()
        {
            var methodCall = new MethodCall("");

            Assert.True(methodCall.IsReceiveHandlerCall);
            Assert.Equal(MethodCall.ReceiveHandlerName, methodCall.Name);
        }

        [Fact]
        public void MethodCall_Should_Not_Return_Internal_Receive_Name()
        {
            var methodCall = new MethodCall(null);

            Assert.False(methodCall.IsReceiveHandlerCall);
            Assert.NotEqual(MethodCall.ReceiveHandlerName, methodCall.Name);
        }
    }
}