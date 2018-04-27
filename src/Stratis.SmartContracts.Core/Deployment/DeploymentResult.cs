using System.Collections.Generic;

namespace Stratis.SmartContracts.Core.Deployment
{
    public class DeploymentResult
    {
        private DeploymentResult(string contractAddress)
        {
            this.Success = true;
            this.ContractAddress = contractAddress;
        }

        private DeploymentResult(IEnumerable<string> errors)
        {
            this.Success = false;
            this.Errors = errors;
        }

        public IEnumerable<string> Errors { get; }

        public string ContractAddress { get; }

        public bool Success { get; }

        public static DeploymentResult DeploymentSuccess(string contractAddress)
        {
            return new DeploymentResult(contractAddress);
        }

        public static DeploymentResult DeploymentFailure(IEnumerable<string> errors)
        {
            return new DeploymentResult(errors);
        }

        public static DeploymentResult DeploymentFailure(string error)
        {
            return new DeploymentResult(new [] { error });
        }
    }
}