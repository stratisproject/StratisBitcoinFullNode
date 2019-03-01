using NBitcoin;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.SourceChain
{
    public class Deposit : IDeposit, IBitcoinSerializable
    {
        public Deposit(uint256 id, Money amount, string targetAddress, int blockNumber, uint256 blockHash)
        {
            this.id = id;
            this.amount = amount;
            this.TargetAddress = targetAddress;
            this.BlockNumber = blockNumber;
            this.BlockHash = blockHash;
        }

        /// <inheritdoc />
        public uint256 Id { get { return this.id; } }
        private uint256 id;

        /// <inheritdoc />
        public Money Amount
        {
            get { return this.amount; }
        }

        private Money amount;

        /// <inheritdoc />
        public string TargetAddress { get; }

        private string targetAddress;

        /// <inheritdoc />
        public int BlockNumber { get; }

        private int blockNumber;

        /// <inheritdoc />
        public uint256 BlockHash { get; }

        private uint256 blockHash;

        /// <inheritdoc />
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.id);

            // Dirty hack to save 'Money'
            ulong amountUlong = this.amount;
            stream.ReadWrite(ref amountUlong);
            this.amount = amountUlong;

            stream.ReadWrite(ref this.targetAddress);
            stream.ReadWrite(ref this.blockNumber);
            stream.ReadWrite(ref this.blockHash);
        }
    }
}