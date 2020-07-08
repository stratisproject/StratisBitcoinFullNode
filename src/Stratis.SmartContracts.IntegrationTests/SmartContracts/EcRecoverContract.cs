
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

        public void CheckThirdPartySignature()
        {
            byte[] message = new byte[] { 0, 1, 2, 3 };
            Address signerOfMessage = this.EcRecover()
        }


}
