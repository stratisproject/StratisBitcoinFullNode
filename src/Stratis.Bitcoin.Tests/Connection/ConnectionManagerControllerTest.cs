using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Connection;
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
            this.connectionManager.Setup(i => i.Network).Returns(this.Network);
            var peerBanning = new Mock<IPeerBanning>();
            this.controller = new ConnectionManagerController(this.connectionManager.Object, peerBanning.Object, this.LoggerFactory.Object);
        }

        [Fact]
        public async Task AddNodeAPI_InvalidCommand_ThrowsArgumentExceptionAsync()
        {
            string endpoint = "0.0.0.0";
            string command = "notarealcommand";

            IActionResult result = await this.controller.AddNodeAPI(endpoint, command);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
            Assert.StartsWith("System.ArgumentException", error.Description);
        }

        [Fact]
        public async Task AddNodeAPI_InvalidEndpoint_ThrowsExceptionAsync()
        {
            string endpoint = "-1.0.0.0";
            string command = "onetry";

            IActionResult result = await this.controller.AddNodeAPI(endpoint, command);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);
            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }

        [Fact]
        public async Task AddNodeAPI_ValidCommand_ReturnsTrueAsync()
        {
            string endpoint = "0.0.0.0";
            string command = "remove";

            var json = (JsonResult)await this.controller.AddNodeAPI(endpoint, command);

            Assert.True((bool)json.Value);
        }
    }
}
