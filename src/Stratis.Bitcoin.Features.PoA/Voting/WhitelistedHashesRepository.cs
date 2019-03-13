using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class WhitelistedHashesRepository
    {
        private const string dbKey = "hashesList";

        private readonly IKeyValueRepository kvRepository;

        /// <summary>Protects access to <see cref="whitelistedHashes"/>.</summary>
        private readonly object locker;

        private readonly ILogger logger;

        private List<uint256> whitelistedHashes;

        public WhitelistedHashesRepository(ILoggerFactory loggerFactory, IKeyValueRepository kvRepository)
        {
            this.kvRepository = kvRepository;
            this.locker = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            lock (this.locker)
            {
                this.whitelistedHashes = this.kvRepository.LoadValueJson<List<uint256>>(dbKey) ?? new List<uint256>();
            }
        }

        private void SaveHashes()
        {
            lock (this.locker)
            {
                this.kvRepository.SaveValueJson(dbKey, this.whitelistedHashes);
            }
        }

        public void AddHash(uint256 hash)
        {
            lock (this.locker)
            {
                if (this.whitelistedHashes.Contains(hash))
                {
                    this.logger.LogTrace("(-)[ALREADY_EXISTS]");
                    return;
                }

                this.whitelistedHashes.Add(hash);

                this.SaveHashes();
            }
        }

        public void RemoveHash(uint256 hash)
        {
            lock (this.locker)
            {
                bool removed = this.whitelistedHashes.Remove(hash);

                if (removed)
                    this.SaveHashes();
            }
        }

        public List<uint256> GetHashes()
        {
            lock (this.locker)
            {
                return new List<uint256>(this.whitelistedHashes);
            }
        }
    }
}
