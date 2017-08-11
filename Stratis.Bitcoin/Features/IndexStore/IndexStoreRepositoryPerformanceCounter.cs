using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreRepositoryPerformanceCounter : BlockStoreRepositoryPerformanceCounter
    {
        public IndexStoreRepositoryPerformanceCounter()
            : base("IndexStore")
        {
        }
    }

    public class IndexStoreRepositoryPerformanceSnapshot : BlockStoreRepositoryPerformanceSnapshot
    {
        public IndexStoreRepositoryPerformanceSnapshot(long repositoryHitCount, long repositoryMissCount, long repositoryDeleteCount, long repositoryInsertCount) :
            base(repositoryHitCount, repositoryMissCount, repositoryDeleteCount, repositoryInsertCount, "IndexStore")
        {

        }

        public static IndexStoreRepositoryPerformanceSnapshot operator -(IndexStoreRepositoryPerformanceSnapshot end, IndexStoreRepositoryPerformanceSnapshot start)
        {
            var diff = (end as BlockStoreRepositoryPerformanceSnapshot) - (start as BlockStoreRepositoryPerformanceSnapshot);

            return new IndexStoreRepositoryPerformanceSnapshot(diff.TotalRepositoryHitCount, diff.TotalRepositoryMissCount, diff.TotalRepositoryDeleteCount, diff.TotalRepositoryInsertCount)
            {
                Start = diff.Start,
                Taken = diff.Taken
            };            
        }
    }
}
