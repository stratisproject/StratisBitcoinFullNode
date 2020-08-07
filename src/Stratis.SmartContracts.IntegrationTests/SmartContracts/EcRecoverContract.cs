
using Stratis.SmartContracts;

public class EcRecoverContract : SmartContract
{
        public Address ThirdPartySigner
        {
            get
            {
                return this.PersistentState.GetAddress(nameof(this.ThirdPartySigner));
            }
            set
            {
                this.PersistentState.SetAddress(nameof(this.ThirdPartySigner), value);
            }
        }

        public EcRecoverContract(ISmartContractState state, Address thirdPartySigner) : base(state)
        {
            this.ThirdPartySigner = thirdPartySigner;
        }

        public bool CheckThirdPartySignature(byte[] message, byte[] signature)
        {
            Address signerOfMessage = this.EcRecover(message, signature);
            return (signerOfMessage == this.ThirdPartySigner);
        }
}
