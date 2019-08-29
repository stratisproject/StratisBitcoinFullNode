using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public static class WhitelistedContractExtensions
    {
        /// <summary>
        /// Adds a consensus rule ensuring only contracts with hashes that are on the PoA whitelist are able to be deployed.
        /// The PoA feature must be installed for this to function correctly.
        /// </summary>
        /// <param name="options">The smart contract options.</param>
        /// <returns>The options provided.</returns>
        public static SmartContractOptions UsePoAWhitelistedContracts(this SmartContractOptions options)
        {
            IServiceCollection services = options.Services;

            // These may have been registered by UsePoAMempoolRules already.
            services.TryAddSingleton<IWhitelistedHashChecker, WhitelistedHashChecker>();
            services.TryAddSingleton<IContractCodeHashingStrategy, Keccak256CodeHashingStrategy>();

            // Registers an additional contract tx validation consensus rule which checks whether the hash of the contract being deployed is whitelisted.
            services.AddSingleton<IContractTransactionFullValidationRule, AllowedCodeHashLogic>();

            return options;
        }

        /// <summary>
        /// The PoA mempool rules have some additional dependencies that are not available at the time the MempoolFeature is instantiated.
        /// The PoA feature must be installed for this to function correctly.
        /// This should be introduced into the builder prior to the SmartContractMempoolValidator.
        /// </summary>
        public static SmartContractOptions UsePoAMempoolRules(this SmartContractOptions options)
        {
            IServiceCollection services = options.Services;

            // TODO: Where exactly should this get injected? Also, one of the builders injects it twice
            services.AddSingleton<IWhitelistedHashesRepository, WhitelistedHashesRepository>();

            services.AddSingleton<IWhitelistedHashChecker, WhitelistedHashChecker>();
            services.AddSingleton<IContractCodeHashingStrategy, Keccak256CodeHashingStrategy>();

            foreach (var ruleType in options.Network.Consensus.MempoolRules)
                services.AddSingleton(typeof(IMempoolRule), ruleType);

            return options;
        }
    }
}