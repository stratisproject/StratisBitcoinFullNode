using LedgerWallet.HIDProviders;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
    public class HIDLedgerTransport : HIDTransportBase
    {
        const int TAG_APDU = 0x05;

        public static VendorProductIds[] WellKnownLedgerWallets = new VendorProductIds[]
        {
            new VendorProductIds(0x2c97),
            new VendorProductIds(0x2581, 0x3b7c)
        };

        public HIDLedgerTransport(IHIDDevice device) : base(device, null)
        {
        }

        static readonly HIDDeviceTransportRegistry<HIDLedgerTransport> _Registry;
        static HIDLedgerTransport()
        {
            _Registry = new HIDDeviceTransportRegistry<HIDLedgerTransport>((d) => new HIDLedgerTransport(d));
        }

        static readonly UsageSpecification[] _UsageSpecification = new[] { new UsageSpecification(0xffa0, 0x01) };
        public static Task<IEnumerable<HIDLedgerTransport>> GetHIDTransportsAsync(IEnumerable<VendorProductIds> ids = null, CancellationToken cancellation = default(CancellationToken))
        {
            ids = ids ?? WellKnownLedgerWallets;
            return _Registry.GetHIDTransportsAsync(ids, _UsageSpecification, cancellation);
        }

        const int DEFAULT_LEDGER_CHANNEL = 0x0101;
        const int LEDGER_HID_PACKET_SIZE = 64;

        protected override byte[] WrapCommandAPDU(Stream command, ref int sequenceIdx)
        {
            var output = new MemoryStream();
            var position = (int)output.Position;
            output.WriteByte((DEFAULT_LEDGER_CHANNEL >> 8) & 0xff);
            output.WriteByte(DEFAULT_LEDGER_CHANNEL & 0xff);
            output.WriteByte(TAG_APDU);
            output.WriteByte((byte)((sequenceIdx >> 8) & 0xff));
            output.WriteByte((byte)(sequenceIdx & 0xff));
            if(sequenceIdx == 0)
            {
                output.WriteByte((byte)((command.Length >> 8) & 0xff));
                output.WriteByte((byte)(command.Length & 0xff));
            }
            sequenceIdx++;
            var headerSize = (int)(output.Position - position);
            var blockSize = Math.Min(LEDGER_HID_PACKET_SIZE - headerSize, (int)command.Length - (int)command.Position);

            var commantPart = command.ReadBytes(blockSize);
            output.Write(commantPart, 0, commantPart.Length);
            while((output.Length % LEDGER_HID_PACKET_SIZE) != 0)
                output.WriteByte(0);
            return output.ToArray();
        }

        protected override byte[] UnwrapReponseAPDU(byte[] data, ref int sequenceIdx, ref int remaining)
        {
            var output = new MemoryStream();
            var input = new MemoryStream(data);
            var position = (int)input.Position;
            var channel = input.ReadBytes(2);
            if(input.ReadByte() != TAG_APDU)
                return null;
            if(input.ReadByte() != ((sequenceIdx >> 8) & 0xff))
                return null;
            if(input.ReadByte() != (sequenceIdx & 0xff))
                return null;

            if(sequenceIdx == 0)
            {
                remaining = ((input.ReadByte()) << 8);
                remaining |= input.ReadByte();
            }
            sequenceIdx++;
            var headerSize = input.Position - position;
            var blockSize = (int)Math.Min(remaining, LEDGER_HID_PACKET_SIZE - headerSize);

            var commandPart = new byte[blockSize];
            if(input.Read(commandPart, 0, commandPart.Length) != commandPart.Length)
                return null;
            output.Write(commandPart, 0, commandPart.Length);
            remaining -= blockSize;
            return output.ToArray();
        }
    }

}
