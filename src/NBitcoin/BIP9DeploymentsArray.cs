namespace NBitcoin
{
    /// <summary>
    /// Interface for recording deployment parameters and returning deployment flags.
    /// </summary>
    public interface IBIP9DeploymentsArray
    {
        void GetFlags(int deployment, out ScriptVerify scriptFlags, out Transaction.LockTimeFlags lockTimeFlags);
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

        public abstract void GetFlags(int deployment, out ScriptVerify scriptFlags, out Transaction.LockTimeFlags lockTimeFlags);

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

        public override void GetFlags(int deployment, out ScriptVerify scriptFlags, out Transaction.LockTimeFlags lockTimeFlags)
        {
            scriptFlags = ScriptVerify.None;
            lockTimeFlags = Transaction.LockTimeFlags.None;
        }
    }
}