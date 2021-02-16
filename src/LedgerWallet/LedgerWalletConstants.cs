using NBitcoin.DataEncoders;

namespace LedgerWallet
{
    public class LedgerWalletConstants
    {
        public const byte LedgerWallet_CLA = 224;

        public const byte LedgerWallet_ADM_CLA = 208;

        public const byte LedgerWallet_BTCHIP_INS_GET_COIN_VER = 22;

        public const byte LedgerWallet_INS_SETUP = 32;

        public const byte LedgerWallet_INS_VERIFY_PIN = 34;

        public const byte LedgerWallet_INS_GET_OPERATION_MODE = 36;

        public const byte LedgerWallet_INS_SET_OPERATION_MODE = 38;

        public const byte LedgerWallet_INS_SET_KEYMAP = 40;

        public const byte LedgerWallet_INS_SET_COMM_PROTOCOL = 42;

        public const byte LedgerWallet_INS_GET_WALLET_PUBLIC_KEY = 64;

        public const byte LedgerWallet_INS_GET_TRUSTED_INPUT = 66;

        public const byte LedgerWallet_INS_HASH_INPUT_START = 68;

        public const byte LedgerWallet_INS_HASH_INPUT_FINALIZE = 70;

        public const byte LedgerWallet_INS_HASH_SIGN = 72;

        public const byte LedgerWallet_INS_HASH_INPUT_FINALIZE_FULL = 74;

        public const byte LedgerWallet_INS_GET_INTERNAL_CHAIN_INDEX = 76;

        public const byte LedgerWallet_INS_SIGN_MESSAGE = 78;

        public const byte LedgerWallet_INS_GET_TRANSACTION_LIMIT = 160;

        public const byte LedgerWallet_INS_SET_TRANSACTION_LIMIT = 162;

        public const byte LedgerWallet_INS_IMPORT_PRIVATE_KEY = 176;

        public const byte LedgerWallet_INS_GET_PUBLIC_KEY = 178;

        public const byte LedgerWallet_INS_DERIVE_BIP32_KEY = 180;

        public const byte LedgerWallet_INS_SIGNVERIFY_IMMEDIATE = 182;

        public const byte LedgerWallet_INS_GET_RANDOM = 192;

        public const byte LedgerWallet_INS_GET_ATTESTATION = 194;

        public const byte LedgerWallet_INS_GET_FIRMWARE_VERSION = 196;

        public const byte LedgerWallet_INS_COMPOSE_MOFN_ADDRESS = 198;

        public const byte LedgerWallet_INS_GET_POS_SEED = 202;

        public const byte LedgerWallet_INS_ADM_SET_KEYCARD_SEED = 38;

        public const int SW_OK = 36864;

        public const int SW_INS_NOT_SUPPORTED = 27904;

        public const int SW_WRONG_P1_P2 = 27392;

        public const int SW_INCORRECT_P1_P2 = 27270;

        public static byte[] QWERTY_KEYMAP = Encoders.Hex.DecodeData("000000000000000000000000760f00d4ffffffc7000000782c1e3420212224342627252e362d3738271e1f202122232425263333362e37381f0405060708090a0b0c0d0e0f101112131415161718191a1b1c1d2f3130232d350405060708090a0b0c0d0e0f101112131415161718191a1b1c1d2f313035");

        public static byte[] AZERTY_KEYMAP = Encoders.Hex.DecodeData("08000000010000200100007820c8ffc3feffff07000000002c38202030341e21222d352e102e3637271e1f202122232425263736362e37101f1405060708090a0b0c0d0e0f331112130415161718191d1b1c1a2f64302f2d351405060708090a0b0c0d0e0f331112130415161718191d1b1c1a2f643035");
    }
}
