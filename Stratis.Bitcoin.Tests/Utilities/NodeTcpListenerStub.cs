using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Wallet.Tests")]
namespace Stratis.Bitcoin.Tests.Utilities
{
    internal class NodeTcpListenerStub : IDisposable
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