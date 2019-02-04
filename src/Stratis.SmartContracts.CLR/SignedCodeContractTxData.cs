using NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Fields that are serialized and sent as data with a smart contract transaction. Includes a digital signature used to authenticate
    /// the contract code provided.
    /// </summary>
    public class SignedCodeContractTxData : ContractTxData
    {
        public SignedCodeContractTxData(int vmVersion, ulong gasPrice, RuntimeObserver.Gas gasLimit, uint160 contractAddress, 
            string method, object[] methodParameters = null) : base(vmVersion, gasPrice, gasLimit, contractAddress, method, methodParameters)
        {
        }

        public SignedCodeContractTxData(int vmVersion, ulong gasPrice, RuntimeObserver.Gas gasLimit, byte[] code, 
            byte[] codeSignature,
            object[] methodParameters = null) : base(vmVersion, gasPrice, gasLimit, code, methodParameters)
        {
            this.CodeSignature = codeSignature;
        }

        /// <summary>
        /// Digital signature used to authenticate the contract execution code provided.
        /// </summary>
        public byte[] CodeSignature { get; }
    }
}