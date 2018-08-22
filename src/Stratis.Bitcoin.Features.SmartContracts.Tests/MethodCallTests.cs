using Stratis.SmartContracts.Executor.Reflection;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class MethodCallTests
    {
        [Fact]
        public void MethodCall_Created_With_Factory_Should_Be_Fallback()
        {
            var methodCall = MethodCall.Fallback();

            Assert.True(methodCall.IsFallbackCall);
        }

        [Fact]
        public void MethodCall_Without_Params_Should_Be_Fallback()
        {
            var methodCall = new MethodCall("");

            Assert.True(methodCall.IsFallbackCall);
        }

        [Fact]
        public void MethodCall_To_Fallback_Should_Be_Fallback()
        {
            var methodCall = new MethodCall("Fallback");

            Assert.True(methodCall.IsFallbackCall);
        }

        [Fact]
        public void MethodCall_To_Fallback_With_Params_Should_Not_Be_Fallback()
        {
            var methodCall = new MethodCall("Fallback", new object[] { 1 });

            Assert.False(methodCall.IsFallbackCall);
        }

        [Fact]
        public void MethodCall_With_Params_Should_Not_Be_Fallback()
        {
            var methodCall = new MethodCall("", new object[] { 1 });

            Assert.False(methodCall.IsFallbackCall);
        }

        [Fact]
        public void MethodCall_Should_Return_Internal_Fallback_Name()
        {
            var methodCall = new MethodCall("");

            Assert.True(methodCall.IsFallbackCall);
            Assert.Equal(MethodCall.FallbackMethodName, methodCall.Name);
        }

        [Fact]
        public void MethodCall_Should_Not_Return_Internal_Fallback_Name()
        {
            var methodCall = new MethodCall(null);

            Assert.False(methodCall.IsFallbackCall);
            Assert.NotEqual(MethodCall.FallbackMethodName, methodCall.Name);
        }
    }
}