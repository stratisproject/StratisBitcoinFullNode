using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public interface IWhitelistedHashesRepository
    {
        void AddHash(uint256 hash);

        void RemoveHash(uint256 hash);

        List<uint256> GetHashes();

        void Initialize();
    }
}