using NBitcoin;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.SourceChain
{
    public class Deposit : IDeposit
    {
        public Deposit(uint256 id, Money amount, string targetAddress, int blockNumber, uint256 blockHash)
        {
            this.Id = id;
            this.Amount = amount;
            this.TargetAddress = targetAddress;
            this.BlockNumber = blockNumber;
            this.BlockHash = blockHash;
        }

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

        /// <inheritdoc />
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}