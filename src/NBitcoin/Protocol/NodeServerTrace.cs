using System;
using System.Diagnostics;

namespace Stratis.Bitcoin.NBitcoin.Protocol
{
    public static class NodeServerTrace
    {
        private static TraceSource trace = new TraceSource("NBitcoin.NodeServer");
        public static TraceSource Trace { get { return trace; } }


    }
}
