using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.ContractSigning;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public static class SignedContractExtensions
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

            services.AddSingleton<IWhitelistedHashChecker, WhitelistedHashChecker>();
            services.AddSingleton<IContractCodeHashingStrategy, Keccak256CodeHashingStrategy>();

            // Registers an additional contract tx validation consensus rule which checks whether the hash of the contract being deployed is whitelisted.
            services.AddSingleton<IContractTransactionFullValidationRule, AllowedCodeHashLogic>();

            return options;
        }

        /// <summary>
        /// Adds a consensus rule ensuring that only contracts which have been signed by a signing authority can be deployed.
        /// Must be registered after the SmartContracts feature is added.
        /// </summary>
        /// <param name="options">The smart contract options.</param>
        /// <param name="signingContractPubKey">The public key of the signing authority.</param>
        /// <returns>The options provided.</returns>
        public static SmartContractOptions UseSignedContracts(this SmartContractOptions options, PubKey signingContractPubKey)
        {
            IServiceCollection services = options.Services;

            // Replace serializer. This is necessary because the new serialized transactions will include a signature.
            services.RemoveAll<ICallDataSerializer>();
            services.AddSingleton<ICallDataSerializer, SignedCodeCallDataSerializer>();
            services.AddSingleton<IContractSigner, ContractSigner>();

            // Add consensus rule.
            services.AddSingleton<IContractTransactionPartialValidationRule>(f => new ContractSignedCodeLogic(f.GetService<IContractSigner>(), signingContractPubKey));

            return options;
        }
    }
}