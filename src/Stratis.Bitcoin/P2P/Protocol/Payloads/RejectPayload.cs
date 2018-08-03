using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using uint256 = NBitcoin.uint256;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    public enum RejectCode : byte
    {
        MALFORMED = 0x01,
        INVALID = 0x10,
        OBSOLETE = 0x11,
        DUPLICATE = 0x12,
        NONSTANDARD = 0x40,
        DUST = 0x41,
        INSUFFICIENTFEE = 0x42,
        CHECKPOINT = 0x43
    }

    public enum RejectCodeType
    {
        Common,
        Version,
        Transaction,
        Block
    }

    /// <summary>
    /// A transaction or block are rejected being transmitted through tx or block messages.
    /// </summary>
    [Payload("reject")]
    public class RejectPayload : Payload
    {
        /// <summary>"tx" or "block".</summary>
        private VarString message = new VarString();

        /// <summary>"tx" or "block".</summary>
        public string Message
        {
            get
            {
                return Encoders.ASCII.EncodeData(this.message.GetString(true));
            }

            set
            {
                this.message = new VarString(Encoders.ASCII.DecodeData(value));
            }
        }

        private byte code;

        public RejectCode Code
        {
            get
            {
                return (RejectCode)this.code;
            }

            set
            {
                this.code = (byte)value;
            }
        }

        /// <summary>Details of the error.</summary>
        private VarString reason = new VarString();

        /// <summary>Details of the error.</summary>
        public string Reason
        {
            get
            {
                return Encoders.ASCII.EncodeData(this.reason.GetString(true));
            }

            set
            {
                this.reason = new VarString(Encoders.ASCII.DecodeData(value));
            }
        }

        /// <summary>The hash being rejected.</summary>
        private uint256 hash;

        /// <summary>The hash being rejected.</summary>
        public uint256 Hash { get { return this.hash; } set { this.hash = value; } }

        public RejectCodeType CodeType
        {
            get
            {
                switch (this.Code)
                {
                    case RejectCode.MALFORMED:
                        return RejectCodeType.Common;

                    case RejectCode.OBSOLETE:
                        if (this.Message == "block") return RejectCodeType.Block;
                        else return RejectCodeType.Version;

                    case RejectCode.DUPLICATE:
                        if (this.Message == "tx") return RejectCodeType.Transaction;
                        else return RejectCodeType.Version;

                    case RejectCode.NONSTANDARD:
                    case RejectCode.DUST:
                    case RejectCode.INSUFFICIENTFEE:
                        return RejectCodeType.Transaction;

                    case RejectCode.CHECKPOINT:
                        return RejectCodeType.Block;

                    case RejectCode.INVALID:
                        if (this.Message == "tx") return RejectCodeType.Transaction;
                        else return RejectCodeType.Block;

                    default:
                        return RejectCodeType.Common;
                }
            }
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.message);
            stream.ReadWrite(ref this.code);
            stream.ReadWrite(ref this.reason);
            if ((this.Message == "tx") || (this.Message == "block"))
                stream.ReadWrite(ref this.hash);
        }
    }
}