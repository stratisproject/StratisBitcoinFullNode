using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Stratis.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// At a point in Stratis' history, the version prefix for extended public key changed, 
    /// however 3rd parties like Ledger didn't update yet.
    /// This class provides a way to convert from the legacy format to the current format.
    /// <remarks>This class can be removed when the 3rd parties update their software.</remarks>
    /// </summary>
    public class LegacyExtPubKeyConverter
    { 
        /// <summary>
        /// Converts a legacy Stratis format into a current Stratis format Base58 extended public key.
        /// </summary>
        /// <param name="extPubKey">The extended public key that may or may not need converting.</param>
        /// <returns></returns>
        public static string ConvertIfInLegacyStratisFormat(string extPubKey, Network network)
        {
            byte[] stratisVersionBytes = network.GetVersionBytes(Base58Type.EXT_PUBLIC_KEY, true);
            byte[] extPubKeyBytes = Encoders.Base58Check.DecodeData(extPubKey);

            if (IsStratisExtPubKey(extPubKeyBytes, stratisVersionBytes))
            {
                return extPubKey;
            }

            if (IsLegacyStratisExtpubKey(extPubKeyBytes))
            {
                return ReplaceBase58Prefix(extPubKeyBytes, network.GetVersionBytes(Base58Type.EXT_PUBLIC_KEY, true));
            }

            throw new FormatException($"ExtPubKey {extPubKey} could not be parsed.");
        }

        private static bool IsStratisExtPubKey(byte[] extPubKey, byte[] stratisVersionBytes)
        {
            byte[] version = extPubKey.Take(4).ToArray();
            return version.SequenceEqual(stratisVersionBytes);
        }

        private static bool IsLegacyStratisExtpubKey(byte[] extPubKey)
        {
            var legacyStratisVersionBytes = new byte[] { (0x04), (0x88), (0xC2), (0x1E) };
            byte[] version = extPubKey.Take(4).ToArray();
            return version.SequenceEqual(legacyStratisVersionBytes);
        }

        private static string ReplaceBase58Prefix(byte[] extPubKeyBytes, byte[] replacement)
        {
            byte[] extPubKeyWithoutVersionsBytes = extPubKeyBytes.Skip(4).ToArray();
            string converted = Encoders.Base58Check.EncodeData(replacement
                .Concat(extPubKeyWithoutVersionsBytes)
                .ToArray());
            return converted;
        }
    }
}