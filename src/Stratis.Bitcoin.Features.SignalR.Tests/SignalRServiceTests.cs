using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Stratis.Bitcoin.Features.SignalR.Tests
{
    public class SignalRServiceTests : IDisposable
    {
        private readonly ISignalRService signalRService;

        public SignalRServiceTests()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger(typeof(SignalRService).FullName).Returns(Substitute.For<ILogger>());
            this.signalRService = new SignalRService(SignalRSettings.Defaults, loggerFactory);
        }

        [Fact]
        public void Test_Address_is_set_to_a_localhost_address()
        {
            Assert.StartsWith("http://localhost:", this.signalRService.Address.AbsoluteUri);
        }

        [Fact]
        public void Test_SendAsync_returns_false_where_service_has_not_been_started()
        {
            var result = this.signalRService.SendAsync(Arg.Any<string>(), Arg.Any<string>());
            Assert.False(result.Result);
        }

        [Fact]
        public void Test_SendAsync_returns_true_where_service_has_been_started()
        {
            var result = false;
            var signal = new ManualResetEvent(false);
            this.signalRService.StartAsync().ContinueWith(_ =>
            {
                result = this.signalRService.SendAsync(Arg.Any<string>(), Arg.Any<string>()).Result;
                signal.Set();
            });
            if (!signal.WaitOne(TimeSpan.FromSeconds(5)))
                result = false;
            Assert.True(result);
        }

        [Fact]
        public void Test_MessageStream_pumps_message_when_SendAsync_is_called()
        {
            var signal = new ManualResetEvent(false);
            const string topic = "thetopic";
            const string data = "thedata";
            (string topic, string data) message = ("", "");
            this.signalRService.MessageStream.Subscribe(x =>
            {
                message = x;
                signal.Set();
            });
            this.signalRService.StartAsync().ContinueWith(_ => this.signalRService.SendAsync(topic, data));
            signal.WaitOne(TimeSpan.FromSeconds(5));
            Assert.Equal(message, (topic, data));
        }

        public void Dispose()
        {
            ((IDisposable)this.signalRService).Dispose();
        }
    }
}
