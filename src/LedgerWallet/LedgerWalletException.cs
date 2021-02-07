using System;

namespace LedgerWallet
{
    public enum WellKnownSW : int
    {
        IncorrectLength = 0x6700,
        SecurityStatusNotSatisfied = 0x6982,
        ConditionsOfUseNotSatisfied = 0x6985,
        InvalidData = 0x6A80,
        FileNotFound = 0x6482,
        IncorrectParameter = 0x6B00,
        OK = 0x9000,
        UnsupportedCommand = 0x6D00
    }
    public class LedgerWalletException : Exception
    {
        public LedgerWalletException(string message) : base(message)
        {

        }
        public LedgerWalletException(string message, LedgerWalletStatus sw)
            : base(message)
        {
            Status = sw ?? throw new ArgumentNullException("sw");
        }
        public LedgerWalletStatus Status { get; }
    }

    public class LedgerWalletStatus
    {
        public LedgerWalletStatus(int sw)
        {
            SW = sw;
        }
        public int SW { get; }

        public WellKnownSW KnownSW
        {
            get
            {
                return (WellKnownSW)SW;
            }
        }

        public int InternalSW
        {
            get
            {
                if((SW & 0xFF00) == 0x6F00)
                    return SW & 0x00FF;
                return 0;
            }
        }
    }
}
