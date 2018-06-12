using System;
using System.Net;

namespace NBitcoin.Protocol
{
    public class NetworkAddress : IBitcoinSerializable
    {
        internal uint ntime;
        private uint version = 100100;

        private ulong service = 1;
        private byte[] ip = new byte[16];
        private ushort port;

        public ulong Service { get { return this.service; } set { this.service = value; } }

        public TimeSpan Ago
        {
            get
            {
                return DateTimeOffset.UtcNow - this.Time;
            }
            set
            {
                this.Time = DateTimeOffset.UtcNow - value;
            }
        }

        public IPEndPoint Endpoint
        {
            get
            {
                return new IPEndPoint(new IPAddress(this.ip), this.port);
            }
            set
            {
                this.port = (ushort)value.Port;
                byte[] ipBytes = value.Address.GetAddressBytes();
                if (ipBytes.Length == 16)
                {
                    this.ip = ipBytes;
                }
                else if (ipBytes.Length == 4)
                {
                    // Convert to ipv4 mapped to ipv6.
                    // In these addresses, the first 80 bits are zero, the next 16 bits are one, and the remaining 32 bits are the IPv4 address.
                    this.ip = new byte[16];
                    Array.Copy(ipBytes, 0, this.ip, 12, 4);
                    Array.Copy(new byte[] { 0xFF, 0xFF }, 0, this.ip, 10, 2);
                }
                else throw new NotSupportedException("Invalid IP address type");
            }
        }

        public DateTimeOffset Time
        {
            get
            {
                return Utils.UnixTimeToDateTime(this.ntime);
            }
            set
            {
                this.ntime = Utils.DateTimeToUnixTime(value);
            }
        }

        public NetworkAddress()
        {
        }

        public NetworkAddress(IPEndPoint endpoint)
        {
            this.Endpoint = endpoint;
        }

        public NetworkAddress(IPAddress address, int port)
        {
            this.Endpoint = new IPEndPoint(address, port);
        }

        public void Adjust()
        {
            uint now = Utils.DateTimeToUnixTime(DateTimeOffset.UtcNow);
            if ((this.ntime <= 100000000) || (this.ntime > now + 10 * 60))
                this.ntime = now - 5 * 24 * 60 * 60;
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            if (stream.Type == SerializationType.Disk)
            {
                stream.ReadWrite(ref this.version);
            }
            if ((stream.Type == SerializationType.Disk)
                || ((stream.ProtocolVersion >= ProtocolVersion.CADDR_TIME_VERSION) && (stream.Type != SerializationType.Hash)))
                stream.ReadWrite(ref this.ntime);

            stream.ReadWrite(ref this.service);
            stream.ReadWrite(ref this.ip);
            using (stream.BigEndianScope())
            {
                stream.ReadWrite(ref this.port);
            }
        }

        #endregion

        public void ZeroTime()
        {
            this.ntime = 0;
        }

        internal byte[] GetKey()
        {
            var key = new byte[18];
            Array.Copy(this.ip, key, 16);
            key[16] = (byte)(this.port / 0x100);
            key[17] = (byte)(this.port & 0x0FF);
            return key;
        }
    }
}