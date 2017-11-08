using System.Net.Sockets;

namespace NBitcoin.Protocol
{
    public class IncomingMessage
    {
        public Message Message { get; set; }
        internal Socket Socket { get; set; }
        public Node Node { get; set; }
        public long Length { get; set; }

        public IncomingMessage()
        {
        }

        public IncomingMessage(Payload payload, Network network)
        {
            this.Message = new Message();
            this.Message.Magic = network.Magic;
            this.Message.Payload = payload;
        }

        internal T AssertPayload<T>() where T : Payload
        {
            if (this.Message.Payload is T)
                return (T)(this.Message.Payload);

            var ex = new ProtocolException("Expected message " + typeof(T).Name + " but got " + this.Message.Payload.GetType().Name);
            throw ex;
        }
    }
}