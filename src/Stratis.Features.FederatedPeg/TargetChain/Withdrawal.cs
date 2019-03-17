using NBitcoin;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class Withdrawal : IWithdrawal
    {
        public Withdrawal(uint256 depositId, uint256 id, Money amount, string targetAddress, int blockNumber, uint256 blockHash)
        {
            this.DepositId = depositId;
            this.Id = id;
            this.Amount = amount;
            this.TargetAddress = targetAddress;
            this.BlockNumber = blockNumber;
            this.BlockHash = blockHash;
        }

        /// <inheritdoc />
        public uint256 DepositId { get; }

        /// <inheritdoc />
        public uint256 Id { get; }

        /// <inheritdoc />
        public Money Amount { get; }

        /// <inheritdoc />
        public string TargetAddress { get; }

        /// <inheritdoc />
        public int BlockNumber { get; }

        /// <inheritdoc />
        public uint256 BlockHash { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public string GetInfo()
        {
            return string.Format("Tran#={0} Dep#={1} Amount={2,12} Addr={3} BlkNum={4,8} BlkHash={5}",
                this.Id.ToString().Substring(0, 6),
                this.DepositId.ToString().Substring(0, 6),
                this.Amount.ToString(),
                this.TargetAddress,
                this.BlockNumber,
                this.BlockHash?.ToString().Substring(0, 6));
        }
    }
}