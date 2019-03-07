using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA.Rules
{
    /// <summary>
    /// Validates that the has of the supplied smart contract code is contained in a list of supplied hashes.
    /// </summary>
    public class AllowedCodeHashLogic : IContractTransactionValidationLogic
    {
        private readonly IWhitelistedHashChecker whitelistedHashChecker;
        private readonly IHashingStrategy hashingStrategy;

        public AllowedCodeHashLogic(IWhitelistedHashChecker whitelistedHashChecker, IHashingStrategy hashingStrategy)
        {
            this.whitelistedHashChecker = whitelistedHashChecker;
            this.hashingStrategy = hashingStrategy;
        }

        public void CheckContractTransaction(ContractTxData txData, Money suppliedBudget)
        {
            throw new System.NotImplementedException();
        }
    }

    public interface IWhitelistedHashChecker
    {
        bool CheckHashWhitelisted(byte[] hash);
    }

    public interface IHashingStrategy
    {
        byte[] Hash(byte[] data);
    }
}