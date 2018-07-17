using System.Collections.Generic;

namespace Stratis.SmartContracts.Core.Deployment
{
    public class DeploymentResult
    {
        public string ContractAddress { get; private set; }
        public string TransactionId { get; private set; }
        public IEnumerable<string> Errors { get; private set; }
        public string Message { get; private set; }
        public bool Success { get; private set; }

        private DeploymentResult()
        {
            this.Errors = new List<string>();
        }

        public static DeploymentResult DeploymentSuccess(BuildCreateContractTransactionResponse response)
        {
            return new DeploymentResult()
            {
                TransactionId = response.TransactionId,
                ContractAddress = response.NewContractAddress,
                Message = response.Message,
                Success = true
            };
        }

        public static DeploymentResult DeploymentFailure(IEnumerable<string> errors)
        {
            return new DeploymentResult() { Errors = errors, Success = false };
        }

        public static DeploymentResult DeploymentFailure(string error)
        {
            return new DeploymentResult() { Errors = new[] { error } };
        }

        public static DeploymentResult DeploymentFailure(BuildCreateContractTransactionResponse response)
        {
            return new DeploymentResult()
            {
                Message = response.Message,
                Success = false
            };
        }
    }
}