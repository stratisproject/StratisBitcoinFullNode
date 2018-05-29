using System.Net;

namespace NBitcoin
{
    public class DNSSeedData
    {
        private IPAddress[] addresses;

        public string Name { get; }

        public string Host { get; }

        public DNSSeedData(string name, string host)
        {
            this.Name = name;
            this.Host = host;
        }

        public IPAddress[] GetAddressNodes()
        {
            if (this.addresses != null)
            {
                return this.addresses;
            }

            this.addresses = Dns.GetHostAddressesAsync(this.Host).GetAwaiter().GetResult();

            return this.addresses;
        }

        public override string ToString()
        {
            return $"{this.Name}({this.Host})";
        }
    }
}
