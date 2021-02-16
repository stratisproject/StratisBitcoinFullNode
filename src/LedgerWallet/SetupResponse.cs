using System.Linq;

namespace LedgerWallet
{
    public class SetupResponse
    {
        public SetupResponse(byte[] bytes)
        {
            SeedTyped = bytes[0] == 1;
            if(bytes.Length == 33)
            {
                TrustedInputKey = new Ledger3DESKey(bytes.Skip(1).Take(16).ToArray());
                WrappingKey = new Ledger3DESKey(bytes.Skip(1 + 16).Take(16).ToArray());
            }
        }

        public bool SeedTyped
        {
            get;
            set;
        }

        public Ledger3DESKey WrappingKey
        {
            get;
            set;
        }

        public Ledger3DESKey TrustedInputKey
        {
            get;
            set;
        }
    }
}
