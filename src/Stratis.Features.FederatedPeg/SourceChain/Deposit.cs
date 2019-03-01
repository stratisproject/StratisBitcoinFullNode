using NBitcoin;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.SourceChain
{
    public class Deposit : IDeposit, IBitcoinSerializable
    {
        // TODO: Use a separate object in the DepositRepository.

        /// <summary>
        /// Needed for deserialization.
        /// </summary>
        public Deposit() { }

        public Deposit(uint256 id, Money amount, string targetAddress, int blockNumber, uint256 blockHash)
        {
            this.id = id;
            this.amount = amount;
            this.targetAddress = targetAddress;
            this.blockNumber = blockNumber;
            this.blockHash = blockHash;
        }

        /// <inheritdoc />
        public uint256 Id { get { return this.id; } }
        private uint256 id;

        /// <inheritdoc />
        public Money Amount => this.amount;
        private Money amount;

        /// <inheritdoc />
        public string TargetAddress => this.targetAddress;
        private string targetAddress;

        /// <inheritdoc />
        public int BlockNumber => this.blockNumber;
        private int blockNumber;

        /// <inheritdoc />
        public uint256 BlockHash => this.blockHash;
        private uint256 blockHash;

        /// <inheritdoc />
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.id);

            // Dirty hack to serialize 'Money'
            ulong amountUlong = this.amount ?? 0;
            stream.ReadWrite(ref amountUlong);
            this.amount = amountUlong;

            stream.ReadWrite(ref this.targetAddress);
            stream.ReadWrite(ref this.blockNumber);
            stream.ReadWrite(ref this.blockHash);
        }
    }
}