using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Tests.Controllers
{
    // API methods call the RPC methods, so can indirectly test RPC through API.
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
                .Returns(KnownNetworks.StratisTest);
            this.controller = new ConnectionManagerController(this.connectionManager.Object, this.LoggerFactory.Object);
        }

        [Fact]
        public void AddNodeAPI_InvalidCommand_ThrowsArgumentException()
        {
            string endpoint = "0.0.0.0";
            string command = "notarealcommand";

            IActionResult result = this.controller.AddNodeAPI(endpoint, command);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.ArgumentException", error.Description);
        }

        [Fact]
        public void AddNodeAPI_InvalidEndpoint_ThrowsException()
        {
            string endpoint = "-1.0.0.0";
            string command = "onetry";

            IActionResult result = this.controller.AddNodeAPI(endpoint, command);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }

        [Fact]
        public void AddNodeAPI_ValidCommand_ReturnsTrue()
        {
            string endpoint = "0.0.0.0";
            string command = "remove";

            var json = (JsonResult)this.controller.AddNodeAPI(endpoint, command);

            Assert.True((bool)json.Value);
        }
    }
}
