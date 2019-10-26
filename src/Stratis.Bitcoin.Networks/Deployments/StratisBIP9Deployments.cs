using NBitcoin;

namespace Stratis.Bitcoin.Networks.Deployments
{
    /// <summary>
    /// BIP9 deployments for the Stratis network.
    /// </summary>
    public class StratisBIP9Deployments : BIP9DeploymentsArray
    {
        // The position of each deployment in the deployments array.
        public const int TestDummy = 0;
        public const int ColdStaking = 2;

        // The number of deployments.
        public const int NumberOfDeployments = ColdStaking + 1;

        /// <summary>
        /// Constructs the BIP9 deployments array.
        /// </summary>
        public StratisBIP9Deployments() : base(NumberOfDeployments)
        {
        }

        /// <summary>
        /// Gets the deployment flags to set when the deployment activates.
        /// </summary>
        /// <param name="deployment">The deployment number.</param>
        /// <returns>The deployment flags.</returns>
        public override BIP9DeploymentFlags GetFlags(int deployment)
        {
            var flags = new BIP9DeploymentFlags();

            switch (deployment)
            {
                case ColdStaking:
                    flags.ScriptFlags |= ScriptVerify.CheckColdStakeVerify;
                    break;
            }

            return flags;
        }
    }
}
