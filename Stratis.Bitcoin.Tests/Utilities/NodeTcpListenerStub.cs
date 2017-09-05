using Stratis.Bitcoin.Utilities;
using System;
using System.Net;
using System.Net.Sockets;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class NodeTcpListenerStub : IDisposable
    {
        private TcpListener listener;

        public NodeTcpListenerStub(IPEndPoint endpoint)
        {
            Guard.NotNull(endpoint, nameof(endpoint));

            this.listener = new TcpListener(endpoint);
            this.listener.Start();
        }

        public void Dispose()
        {
            if (this.listener != null)
            {
                this.listener.Stop();
                this.listener = null;
            }
        }
    }
}