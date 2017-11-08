using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace NBitcoin.Protocol
{
    [Payload("alert")]
    public class AlertPayload : Payload, IBitcoinSerializable
    {
        /// <summary>
        /// Used for knowing if an alert is valid in past of future.
        /// </summary>
        public DateTimeOffset? Now { get; set; }

        private VarString payload;
        private VarString signature;

        private int version;
        private long relayUntil;
        private int id;
        private int cancel;
        private int[] setCancel = new int[0];
        private int minVer;
        private int maxVer;
        private VarString[] setSubVer = new VarString[0];
        private int priority;
        private VarString comment;
        private VarString statusBar;
        private VarString reserved;

        private long expiration;
        public DateTimeOffset Expiration
        {
            get
            {
                return Utils.UnixTimeToDateTime((uint)this.expiration);
            }
            set
            {
                this.expiration = Utils.DateTimeToUnixTime(value);
            }
        }

        public string[] SetSubVer
        {
            get
            {
                List<string> messages = new List<string>();
                foreach (VarString ver in this.setSubVer)
                {
                    messages.Add(Encoders.ASCII.EncodeData(ver.GetString()));
                }
                return messages.ToArray();
            }
            set
            {
                List<VarString> messages = new List<VarString>();
                foreach (var v in value)
                {
                    messages.Add(new VarString(Encoders.ASCII.DecodeData(v)));
                }
                this.setSubVer = messages.ToArray();
            }
        }

        public bool IsInEffect
        {
            get
            {
                DateTimeOffset now = this.Now ?? DateTimeOffset.Now;
                return now < this.Expiration;
            }
        }

        public string Comment
        {
            get
            {
                return Encoders.ASCII.EncodeData(this.comment.GetString());
            }
            set
            {
                this.comment = new VarString(Encoders.ASCII.DecodeData(value));
            }
        }

        public string StatusBar
        {
            get
            {
                return Encoders.ASCII.EncodeData(this.statusBar.GetString());
            }
            set
            {
                this.statusBar = new VarString(Encoders.ASCII.DecodeData(value));
            }
        }

        #region IBitcoinSerializable Members

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.payload);
            if (!stream.Serializing)
            {
                var payloadStream = new BitcoinStream(this.payload.GetString());
                payloadStream.CopyParameters(stream);

                this.ReadWritePayloadFields(payloadStream);

            }

            stream.ReadWrite(ref this.signature);
        }

        private void ReadWritePayloadFields(BitcoinStream payloadStream)
        {
            payloadStream.ReadWrite(ref this.version);
            payloadStream.ReadWrite(ref this.relayUntil);
            payloadStream.ReadWrite(ref this.expiration);
            payloadStream.ReadWrite(ref this.id);
            payloadStream.ReadWrite(ref this.cancel);
            payloadStream.ReadWrite(ref this.setCancel);
            payloadStream.ReadWrite(ref this.minVer);
            payloadStream.ReadWrite(ref this.maxVer);
            payloadStream.ReadWrite(ref this.setSubVer);
            payloadStream.ReadWrite(ref this.priority);
            payloadStream.ReadWrite(ref this.comment);
            payloadStream.ReadWrite(ref this.statusBar);
            payloadStream.ReadWrite(ref this.reserved);
        }

        private void UpdatePayload(BitcoinStream stream)
        {
            MemoryStream ms = new MemoryStream();
            var seria = new BitcoinStream(ms, true);
            seria.CopyParameters(stream);
            this.ReadWritePayloadFields(seria);
            this.payload = new VarString(ms.ToArray());
        }

        #endregion

        // FIXME: why do we need version parameter?
        // it shouldn't be called "version" because the it a field with the same name
        public void UpdateSignature(Key key)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            this.UpdatePayload();
            this.signature = new VarString(key.Sign(Hashes.Hash256(this.payload.GetString())).ToDER());
        }

        public void UpdatePayload(ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION)
        {
            this.UpdatePayload(new BitcoinStream(new byte[0])
            {
                ProtocolVersion = protocolVersion
            });
        }

        public bool CheckSignature(Network network)
        {
            if (network == null)
                throw new ArgumentNullException("network");
            return CheckSignature(network.AlertPubKey);
        }

        public bool CheckSignature(PubKey key)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            return key.Verify(Hashes.Hash256(this.payload.GetString()), this.signature.GetString());
        }

        public bool AppliesTo(int version, string subVerIn)
        {
            return this.IsInEffect
                    && (this.minVer <= version) && (version <= this.maxVer)
                    && ((this.SetSubVer.Length == 0) || this.SetSubVer.Contains(subVerIn));
        }

        public override string ToString()
        {
            return this.StatusBar;
        }
    }
}