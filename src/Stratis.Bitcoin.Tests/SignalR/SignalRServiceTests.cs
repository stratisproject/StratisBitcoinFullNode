using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.SignalR;
using Xunit;

namespace Stratis.Bitcoin.Tests.SignalR
{
    public class SignalRServiceTests : IDisposable
    {
        private readonly ISignalRService signalRService;

        public SignalRServiceTests()
        {
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(x => x.CreateLogger(typeof(SignalRService).FullName)).Returns(new Mock<ILogger>().Object);
            this.signalRService = new SignalRService(loggerFactory.Object);
        }

        [Fact]
        public void Test_Address_is_set_to_a_localhost_address()
        {
            Assert.StartsWith("http://localhost:", this.signalRService.Address.AbsoluteUri);
        }

        [Fact]
        public void Test_SendAsync_returns_false_where_service_has_not_been_started()
        {
            var result = this.signalRService.SendAsync(It.IsAny<string>(), It.IsAny<string>());
            Assert.False(result.Result);
        }

        [Fact]
        public void Test_SendAsync_returns_true_where_service_has_been_started()
        {
            var result = false;
            var signal = new ManualResetEvent(false);
            this.signalRService.StartAsync().ContinueWith(_ =>
            {
                result = this.signalRService.SendAsync(It.IsAny<string>(), It.IsAny<string>()).Result;
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
