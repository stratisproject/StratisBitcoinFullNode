using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    /// <summary>
    /// Checks hashes against a whitelist.
    /// </summary>
    public class WhitelistedHashChecker : IWhitelistedHashChecker
    {
        private readonly IWhitelistedHashesRepository whitelistedHashesRepository;

        public WhitelistedHashChecker(IWhitelistedHashesRepository whitelistedHashesRepository)
        {
            this.whitelistedHashesRepository = whitelistedHashesRepository;
        }

        /// <summary>
        /// Checks that a supplied hash is present in the whitelisted hashes repository.
        /// </summary>
        /// <param name="hash">The bytes of the hash to check.</param>
        /// <returns>True if the hash was found in the whitelisted hashes repository.</returns>
        public bool CheckHashWhitelisted(byte[] hash)
        {
            List<uint256> allowedHashes = this.whitelistedHashesRepository.GetHashes();

            uint256 hash256;

            try
            {
                hash256 = new uint256(hash);
            }
            catch
            {
                // Hashes can come from user-generated data and might not always be valid uint256 byte sequences.
                // Because of this, we ignore invalid hashes by swallowing the exception.
                return false;
            }

            return allowedHashes.Contains(hash256);
        }
    }
}