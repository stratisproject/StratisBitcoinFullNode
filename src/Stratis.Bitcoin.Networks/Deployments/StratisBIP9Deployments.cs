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
        public const int CSV = 1;

        // The number of deployments.
        public const int NumberOfDeployments = 2;

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
        /// <param name="scriptFlags">The script flags to set.</param>
        /// <param name="lockTimeFlags">The lock time flags to set.</param>
        public override void GetFlags(int deployment, out ScriptVerify scriptFlags, out Transaction.LockTimeFlags lockTimeFlags)
        {
            scriptFlags = ScriptVerify.None;
            lockTimeFlags = Transaction.LockTimeFlags.None;

            switch (deployment)
            {
                case CSV:
                    // Start enforcing BIP68 (sequence locks), BIP112 (CHECKSEQUENCEVERIFY) and BIP113 (Median Time Past) using versionbits logic.
                    scriptFlags = ScriptVerify.CheckSequenceVerify;
                    lockTimeFlags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;
                    break;
            }
        }
    }
}
