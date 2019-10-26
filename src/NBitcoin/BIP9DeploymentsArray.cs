namespace NBitcoin
{
    /// <summary>
    /// Contains the <see cref="ScriptVerify" /> and <see cref="Transaction.LockTimeFlags" /> flags to set
    /// when the deployment goes active.
    /// </summary>
    public class BIP9DeploymentFlags
    {
        public ScriptVerify ScriptFlags { get; set; }
        public Transaction.LockTimeFlags LockTimeFlags { get; set; }

        public BIP9DeploymentFlags()
        {
            this.ScriptFlags = ScriptVerify.None;
            this.LockTimeFlags = Transaction.LockTimeFlags.None;
        }
    }

    /// <summary>
    /// Interface for recording deployment parameters and returning deployment flags.
    /// </summary>
    public interface IBIP9DeploymentsArray
    {
        /// <summary>The number of elements/deployments in the array.</summary>
        int Length { get; }

        /// <summary>
        /// Gets the flags to set when the deployment goes active.
        /// </summary>
        /// <param name="deployment">The deployment number (element index in array).</param>
        /// <returns>The flags to set.</returns>
        BIP9DeploymentFlags GetFlags(int deployment);

        /// <summary>
        /// Gets or sets the deployment parameters for a deployment.
        /// </summary>
        /// <param name="deployment">The deployment number (element index in array).</param>
        /// <returns>The deployment parameters if this is a get.</returns>
        BIP9DeploymentsParameters this[int deployment] { get; set; }
    }

    /// <summary>
    /// Used for recording deployment parameters and returning deployment flags.
    /// </summary>
    public abstract class BIP9DeploymentsArray : IBIP9DeploymentsArray
    {
        protected readonly BIP9DeploymentsParameters[] parameters;

        /// <summary>
        /// Constructs a deployments array of the given length.
        /// </summary>
        /// <param name="length">The length of the deployments array to construct.</param>
        public BIP9DeploymentsArray(int length)
        {
            this.parameters = new BIP9DeploymentsParameters[length];
        }

        /// <inheritdoc />
        public abstract BIP9DeploymentFlags GetFlags(int deployment);

        /// <inheritdoc />
        public BIP9DeploymentsParameters this[int deployment]
        {
            get { return this.parameters[deployment]; }
            set { this.parameters[deployment] = value; }
        }

        /// <inheritdoc />
        public int Length => this.parameters.Length;
    }

    /// <summary>
    /// Used by networks that don't define any deployments or deployment parameters.
    /// </summary>
    public class NoBIP9Deployments : BIP9DeploymentsArray
    {
        /// <summary>
        /// Constructs a zero-length deployments array.
        /// </summary>
        public NoBIP9Deployments() : base(0)
        {
        }

        /// <inheritdoc />
        public override BIP9DeploymentFlags GetFlags(int deployment)
        {
            return new BIP9DeploymentFlags();
        }
    }
}