using NBitcoin;

namespace Stratis.Bitcoin.Networks.Deployments
{
    /// <summary>
    /// BIP9 deployments for the Stratis network.
    /// </summary>
    public class StratisBIP9Deployments : BIP9DeploymentsArray
    {
        // The position of each deployment in the deployments array. Note that this is decoupled from the actual position of the flag bit for the deployment in the block version.
        public const int TestDummy = 0;
        public const int CSV = 1;
        public const int Segwit = 2;
        public const int ColdStaking = 3;

        // The number of deployments.
        public const int NumberOfDeployments = 4;

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

                case CSV:
                    // Start enforcing BIP68 (sequence locks), BIP112 (CHECKSEQUENCEVERIFY) and BIP113 (Median Time Past) using versionbits logic.
                    flags.ScriptFlags = ScriptVerify.CheckSequenceVerify;
                    flags.LockTimeFlags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;
                    break;

                case Segwit:
                    // Start enforcing WITNESS rules using versionbits logic.
                    flags.ScriptFlags = ScriptVerify.Witness;
                    break;
            }

            return flags;
        }
    }
}