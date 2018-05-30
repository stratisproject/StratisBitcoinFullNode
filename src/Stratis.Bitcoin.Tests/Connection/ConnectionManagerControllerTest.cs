using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Tests.Controllers
{
    public class ConnectionManagerControllerTest : LogsTestBase
    {
        private readonly Mock<IConnectionManager> connectionManager;
        private ConnectionManagerController controller;
        private readonly Mock<ILoggerFactory> mockLoggerFactory;

        public ConnectionManagerControllerTest()
        {
            this.connectionManager = new Mock<IConnectionManager>();
            this.mockLoggerFactory = new Mock<ILoggerFactory>();
            this.mockLoggerFactory.Setup(i => i.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
            this.connectionManager.Setup(i => i.Network)
                .Returns(Network.StratisTest);
            this.controller = new ConnectionManagerController(this.connectionManager.Object, this.LoggerFactory.Object);
        }

        [Fact]
        public void AddNode_InvalidCommand_ThrowsArgumentException()
        {
            AddNodeRequestModel request = new AddNodeRequestModel
            {
                Endpoint = "0.0.0.0",
                Command = "notarealcommand"
            };

            IActionResult result = this.controller.AddNode(request);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.ArgumentException", error.Description);
        }

        [Fact]
        public void AddNode_InvalidEndpoint_ThrowsException()
        {
            AddNodeRequestModel request = new AddNodeRequestModel
            {
                Endpoint = "a.b.c.d",
                Command = "onetry"
            };

            IActionResult result = this.controller.AddNode(request);

            ErrorResult errorResult = Assert.IsType<ErrorResult>(result);
            ErrorResponse errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }

        [Fact]
        public void AddNode_ValidCommand_ReturnsTrue()
        {
            AddNodeRequestModel request = new AddNodeRequestModel
            {
                Endpoint = "0.0.0.0",
                Command = "remove"
            };

            var json = (JsonResult)this.controller.AddNode(request);

            Assert.True((bool)json.Value);
        }
    }
}
