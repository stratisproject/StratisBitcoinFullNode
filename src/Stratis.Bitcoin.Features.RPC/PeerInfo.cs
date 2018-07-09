using System;
using System.Net;

namespace Stratis.Bitcoin.Features.RPC
{
    public class PeerInfo
    {
        public int Id { get; internal set; }
        public IPEndPoint Address { get; internal set; }
        public IPEndPoint LocalAddress { get; internal set; }
        public ulong Services { get; internal set; }
        public DateTimeOffset LastSend { get; internal set; }
        public DateTimeOffset LastReceive { get; internal set; }
        public long BytesSent { get; internal set; }
        public long BytesReceived { get; internal set; }
        public DateTimeOffset ConnectionTime { get; internal set; }
        public TimeSpan? PingTime { get; internal set; }
        public int Version { get; internal set; }
        public string SubVersion { get; internal set; }
        public bool Inbound { get; internal set; }
        public int StartingHeight { get; internal set; }
        public int BanScore { get; internal set; }
        public int SynchronizedHeaders { get; internal set; }
        public int SynchronizedBlocks { get; internal set; }
        public uint[] Inflight { get; internal set; }
        public bool IsWhiteListed { get; internal set; }
        public TimeSpan PingWait { get; internal set; }
        public int Blocks { get; internal set; }
        public TimeSpan TimeOffset { get; internal set; }
    }
}