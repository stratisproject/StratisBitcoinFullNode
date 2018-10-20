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
        BIP9DeploymentFlags GetFlags(int deployment);
        int Length { get; }
        BIP9DeploymentsParameters this[int deployment] { get; set; }
    }

    /// <summary>
    /// Used for recording deployment parameters and returning deployment flags.
    /// </summary>
    public abstract class BIP9DeploymentsArray : IBIP9DeploymentsArray
    {
        protected readonly BIP9DeploymentsParameters[] parameters;

        public BIP9DeploymentsArray(int length)
        {
            this.parameters = new BIP9DeploymentsParameters[length];
        }

        public abstract BIP9DeploymentFlags GetFlags(int deployment);

        public BIP9DeploymentsParameters this[int deployment]
        {
            get { return this.parameters[deployment]; }
            set { this.parameters[deployment] = value; }
        }

        public int Length => this.parameters.Length;
    }

    /// <summary>
    /// Used by networks that don't define any deployments or deployment pararameters.
    /// </summary>
    public class NoBIP9Deployments : BIP9DeploymentsArray
    {
        public NoBIP9Deployments() : base(0)
        {
        }

        public override BIP9DeploymentFlags GetFlags(int deployment)
        {
            return new BIP9DeploymentFlags();
        }
    }
}