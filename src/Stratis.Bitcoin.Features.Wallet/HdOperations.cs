using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Class providing helper methods for working with Hierarchical Deterministic (HD) wallets.
    /// </summary>
    /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki" />
    public class HdOperations
    {
        /// <summary>
        /// Generates an HD public key derived from an extended public key.
        /// </summary>
        /// <param name="accountExtPubKey">The extended public key used to generate child keys.</param>
        /// <param name="index">The index of the child key to generate.</param>
        /// <param name="isChange">A value indicating whether the public key to generate corresponds to a change address.</param>
        /// <returns>
        /// An HD public key derived from an extended public key.
        /// </returns>
        public static PubKey GeneratePublicKey(string accountExtPubKey, int index, bool isChange)
        {
            Guard.NotEmpty(accountExtPubKey, nameof(accountExtPubKey));

            int change = isChange ? 1 : 0;
            var keyPath = new KeyPath($"{change}/{index}");
            ExtPubKey extPubKey = ExtPubKey.Parse(accountExtPubKey).Derive(keyPath);
            return extPubKey.PubKey;
        }

        /// <summary>
        /// Gets the extended private key for an account.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended private key.</param>
        /// <param name="chainCode">The chain code used in creating the extended private key.</param>
        /// <param name="hdPath">The HD path of the account for which to get the extended private key.</param>
        /// <param name="network">The network for which to generate this extended private key.</param>
        /// <returns></returns>
        [NoTrace]
        public static ISecret GetExtendedPrivateKey(Key privateKey, byte[] chainCode, string hdPath, Network network)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));
            Guard.NotEmpty(hdPath, nameof(hdPath));
            Guard.NotNull(network, nameof(network));

            // Get the extended key.
            var seedExtKey = new ExtKey(privateKey, chainCode);
            ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(hdPath));
            BitcoinExtKey addressPrivateKey = addressExtKey.GetWif(network);
            return addressPrivateKey;
        }

        /// <summary>
        /// Gets the extended public key for an account.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended public key.</param>
        /// <param name="chainCode">The chain code used in creating the extended public key.</param>
        /// <param name="coinType">Type of the coin of the account for which to generate an extended public key.</param>
        /// <param name="accountIndex">Index of the account for which to generate an extended public key.</param>
        /// <returns>The extended public key for an account, used to derive child keys.</returns>
        public static ExtPubKey GetExtendedPublicKey(Key privateKey, byte[] chainCode, int coinType, int accountIndex)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));

            string accountHdPath = GetAccountHdPath(coinType, accountIndex);
            return GetExtendedPublicKey(privateKey, chainCode, accountHdPath);
        }

        /// <summary>
        /// Gets the extended public key corresponding to an HD path.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended public key.</param>
        /// <param name="chainCode">The chain code used in creating the extended public key.</param>
        /// <param name="hdPath">The HD path for which to get the extended public key.</param>
        /// <returns>The extended public key, used to derive child keys.</returns>
        public static ExtPubKey GetExtendedPublicKey(Key privateKey, byte[] chainCode, string hdPath)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));
            Guard.NotEmpty(hdPath, nameof(hdPath));

            // get extended private key
            var seedExtKey = new ExtKey(privateKey, chainCode);
            ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(hdPath));
            ExtPubKey extPubKey = addressExtKey.Neuter();
            return extPubKey;
        }

        /// <summary>
        /// Gets the HD path of an account.
        /// </summary>
        /// <param name="coinType">Type of the coin this account is in.</param>
        /// <param name="accountIndex">Index of the account.</param>
        /// <returns>The HD path of an account.</returns>
        public static string GetAccountHdPath(int coinType, int accountIndex)
        {
            return $"m/44'/{coinType}'/{accountIndex}'";
        }

        /// <summary>
        /// Gets the extended key generated by this mnemonic and passphrase.
        /// </summary>
        /// <param name="mnemonic">The mnemonic used to generate the key.</param>
        /// <param name="passphrase">The passphrase used in generating the key.</param>
        /// <returns>The extended key generated by this mnemonic and passphrase.</returns>
        /// <remarks>This key is sometimes referred to as the 'root seed' or the 'master key'.</remarks>
        public static ExtKey GetExtendedKey(string mnemonic, string passphrase = null)
        {
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            return GetExtendedKey(new Mnemonic(mnemonic), passphrase);
        }

        /// <summary>
        /// Gets the extended key generated by this mnemonic and passphrase.
        /// </summary>
        /// <param name="mnemonic">The mnemonic used to generate the key.</param>
        /// <param name="passphrase">The passphrase used in generating the key.</param>
        /// <returns>The extended key generated by this mnemonic and passphrase.</returns>
        /// <remarks>This key is sometimes referred to as the 'root seed' or the 'master key'.</remarks>
        public static ExtKey GetExtendedKey(Mnemonic mnemonic, string passphrase = null)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));

            return mnemonic.DeriveExtKey(passphrase);
        }

        /// <summary>
        /// Creates an address' HD path, according to BIP 44.
        /// </summary>
        /// <param name="coinType">Type of coin in the HD path.</param>
        /// <param name="accountIndex">Index of the account in the HD path.</param>
        /// <param name="isChange">A value indicating whether the HD path to generate corresponds to a change address.</param>
        /// <param name="addressIndex">Index of the address in the HD path.</param>
        /// <returns>The HD path.</returns>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        public static string CreateHdPath(int coinType, int accountIndex, bool isChange, int addressIndex)
        {
            int change = isChange ? 1 : 0;
            return $"m/44'/{coinType}'/{accountIndex}'/{change}/{addressIndex}";
        }

        /// <summary>
        /// Gets the type of coin this HD path is for.
        /// </summary>
        /// <param name="hdPath">The HD path.</param>
        /// <returns>The type of coin. <seealso cref="https://github.com/satoshilabs/slips/blob/master/slip-0044.md"/>.</returns>
        /// <exception cref="FormatException">An exception is thrown if the HD path is not well-formed.</exception>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        public static int GetCoinType(string hdPath)
        {
            Guard.NotEmpty(hdPath, nameof(hdPath));

            string[] pathElements = hdPath.Split('/');
            if (pathElements.Length < 3)
                throw new FormatException($"Could not parse CoinType from HdPath {hdPath}.");

            int coinType = 0;
            if (int.TryParse(pathElements[2].Replace("'", string.Empty), out coinType))
            {
                return coinType;
            }

            throw new FormatException($"Could not parse CoinType from HdPath {hdPath}.");
        }

        /// <summary>
        /// Determines whether the HD path corresponds to a change address.
        /// </summary>
        /// <param name="hdPath">The HD path.</param>
        /// <returns>A value indicating if the HD path corresponds to a change address.</returns>
        /// <exception cref="FormatException">An exception is thrown if the HD path is not well-formed.</exception>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        [NoTrace]
        public static bool IsChangeAddress(string hdPath)
        {
            Guard.NotEmpty(hdPath, nameof(hdPath));

            string[] hdPathParts = hdPath.Split('/');
            if (hdPathParts.Length < 5)
                throw new FormatException($"Could not parse value from HdPath {hdPath}.");

            int result = 0;
            if (int.TryParse(hdPathParts[4], out result))
            {
                return result == 1;
            }

            return false;
        }

        /// <summary>
        /// Decrypts the encrypted private key (seed).
        /// </summary>
        /// <param name="encryptedSeed">The encrypted seed to decrypt.</param>
        /// <param name="password">The password used to decrypt the encrypted seed.</param>
        /// <param name="network">The network this seed applies to.</param>
        /// <returns></returns>
        public static Key DecryptSeed(string encryptedSeed, string password, Network network)
        {
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(network, nameof(network));

            return Key.Parse(encryptedSeed, password, network);
        }
    }
}
