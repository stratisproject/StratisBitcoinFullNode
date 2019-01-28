namespace Stratis.SmartContracts.RuntimeObserver
{
    /// <summary>
    /// Contract that defines how gas is spent.
    /// </summary>
    public interface IGasMeter
    {
        /// <summary>The amount of gas left that the contract can spend.</summary>
        Gas GasAvailable { get; }

        /// <summary>The amount of gas already used by the contract.</summary>
        Gas GasConsumed { get; }

        /// <summary>The maximum amount of gas that can be spent by the contract.</summary>
        Gas GasLimit { get; }

        /// <summary>Spends the gas used by the contract.</summary>
        void Spend(Gas spend);
    }
}