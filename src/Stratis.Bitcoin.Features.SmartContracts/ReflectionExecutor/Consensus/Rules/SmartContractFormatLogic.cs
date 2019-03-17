using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules
{
    /// <summary>
    /// Validates that a smart contract transaction can be deserialized correctly, and that it conforms to gas
    /// price and gas limit rules.
    /// </summary>
    public class SmartContractFormatLogic : IContractTransactionValidationLogic
    {
        public const ulong GasLimitMaximum = 100_000;

        public const ulong GasLimitCallMinimum = GasPriceList.BaseCost;

        public const ulong GasLimitCreateMinimum = GasPriceList.CreateCost;

        public const ulong GasPriceMinimum = 1;

        public const ulong GasPriceMaximum = 10_000;

        public void CheckContractTransaction(ContractTxData txData, Money suppliedBudget)
        {
            if (txData.GasPrice < GasPriceMinimum)
            {
                // Supplied gas price is too low.
                this.ThrowGasPriceLessThanMinimum();
            }

            if (txData.GasPrice > GasPriceMaximum)
            {
                // Supplied gas price is too high.
                this.ThrowGasPriceMoreThanMaximum();
            }

            if (txData.IsCreateContract && txData.GasLimit < GasLimitCreateMinimum)
            {
                this.ThrowGasLessThenCreateFee();
            }

            if (!txData.IsCreateContract && txData.GasLimit < GasLimitCallMinimum)
            {
                // Supplied gas limit is too low.
                this.ThrowGasLessThanBaseFee();
            }

            if (txData.GasLimit > GasLimitMaximum)
            {
                // Supplied gas limit is too high - at a certain point we deem that a contract is taking up too much time.
                this.ThrowGasGreaterThanHardLimit();
            }

            // Only measure budget when coming from mempool - this happens inside SmartContractCoinviewRule instead as part of the block.
            if (suppliedBudget != null)
            {
                // Note carrier.GasCostBudget cannot overflow given values are within constraints above.
                if (suppliedBudget < new Money(txData.GasCostBudget))
                {
                    // Supplied satoshis are less than the budget we said we had for the contract execution
                    SmartContractConsensusErrors.FeeTooSmallForGas.Throw();
                }
            }
        }

        private void ThrowGasPriceLessThanMinimum()
        {
            // TODO make nicer
            new ConsensusError("gas-price-less-than-minimum", "gas price supplied is less than minimum allowed: " + GasPriceMinimum).Throw();
        }

        private void ThrowGasPriceMoreThanMaximum()
        {
            // TODO make nicer
            new ConsensusError("gas-price-more-than-maximum", "gas price supplied is more than maximum allowed: " + GasPriceMaximum).Throw();
        }

        private void ThrowGasLessThanBaseFee()
        {
            // TODO make nicer
            new ConsensusError("gas-limit-less-than-base-fee", "gas limit supplied is less than the base fee for contract execution: " + GasLimitCallMinimum).Throw();
        }
        private void ThrowGasLessThenCreateFee()
        {
            // TODO make nicer
            new ConsensusError("gas-limit-less-than-create-fee", "gas limit supplied is less than the base fee for contract creation: " + GasLimitCreateMinimum).Throw();
        }

        private void ThrowGasGreaterThanHardLimit()
        {
            // TODO make nicer
            new ConsensusError("total-gas-value-greater-than-hard-limit", "total supplied gas value was greater than our hard limit of " + GasLimitMaximum).Throw();
        }
    }
}