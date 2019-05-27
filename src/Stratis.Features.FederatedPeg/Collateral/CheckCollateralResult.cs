namespace Stratis.Features.FederatedPeg.Collateral
{
    /// <summary>
    /// Returns information with regards to checking a particular federation member's collateral.
    /// </summary>
    public sealed class CheckCollateralResult
    {
        private CheckCollateralResult() { }

        /// <summary>A flag indicating the <see cref="CollateralChecker"/>'s initialization status.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary><c>True</c> if the federation member's collateral amount is valid.</summary>
        public bool Succeeded { get; private set; }

        /// <summary>Indicates that the the collateral amount of the federation member is not valid.</summary>
        public static CheckCollateralResult Failed()
        {
            return new CheckCollateralResult() { IsInitialized = true };
        }

        /// <summary>Indicates that the collateral checker is not yet initialized.</summary>
        public static CheckCollateralResult NotInitialized()
        {
            return new CheckCollateralResult();
        }

        /// <summary>Indicates that the collateral checker is initialized and that the federation member's collateral amount is valid.</summary>
        public static CheckCollateralResult Passed()
        {
            return new CheckCollateralResult() { IsInitialized = true, Succeeded = true };
        }
    }
}