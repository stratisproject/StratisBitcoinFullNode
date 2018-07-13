using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.Tests.Connection
{

    public class ConnectionManagerTests
    {
        private readonly Mock<ILoggerFactory> loggerFactory;
        public void RemoveNodeAddress_should_remove_endpoint_from_connected_peers()
        {
            //var connectionManager = new ConnectionManager();

        }
    }
}
