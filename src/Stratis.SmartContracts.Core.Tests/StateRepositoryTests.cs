using System.Text;
using DBreeze;
using NBitcoin;
using Stratis.Patricia;
using Stratis.SmartContracts.Core.State;
using Xunit;
using MemoryDictionarySource = Stratis.Patricia.MemoryDictionarySource;

namespace Stratis.SmartContracts.Core.Tests
{
    public class StateRepositoryTests
    {
        private static readonly byte[] empty = new byte[0];
        private static readonly byte[] dog = Encoding.UTF8.GetBytes("dog");
        private static readonly byte[] dodecahedron = Encoding.UTF8.GetBytes("dodecahedron");
        private static readonly byte[] cat = Encoding.UTF8.GetBytes("cat");
        private static readonly byte[] fish = Encoding.UTF8.GetBytes("fish");
        private static readonly byte[] bird = Encoding.UTF8.GetBytes("bird");
        // EthereumJ consts below
        private static readonly byte[] cow = "CD2A3D9F938E13CD947EC05ABC7FE734DF8DD826".HexToByteArray();
        private static readonly byte[] horse = "13978AEE95F38490E9769C39B2773ED763D9CD5F".HexToByteArray();
        private static readonly byte[] cowCode = "A1A2A3".HexToByteArray();
        private static readonly byte[] horseCode = "B1B2B3".HexToByteArray();
        private static readonly byte[] cowKey = "A1A2A3".HexToByteArray();
        private static readonly byte[] cowValue = "A4A5A6".HexToByteArray();
        private static readonly byte[] horseKey = "B1B2B3".HexToByteArray();
        private static readonly byte[] horseValue = "B4B5B6".HexToByteArray();
        private static readonly byte[] cowKey1 = "c1".HexToByteArray();
        private static readonly byte[] cowKey2 = "c2".HexToByteArray();
        private static readonly byte[] cowVal1 = "c0a1".HexToByteArray();
        private static readonly byte[] cowVal0 = "c0a0".HexToByteArray();
        private static readonly byte[] horseKey1 = "e1".HexToByteArray();
        private static readonly byte[] horseKey2 = "e2".HexToByteArray();
        private static readonly byte[] horseVal1 = "c0a1".HexToByteArray();
        private static readonly byte[] horseVal0 = "c0a0".HexToByteArray();

        private static readonly uint160 testAddress = 111111;
        private const string DbreezeTestLocation = "C:/temp";
        private const string DbreezeTestDb = "test";

        // Numbered tests are taken from EthereumJ....RepositoryTests

        [Fact]
        public void Test3()
        {
            StateRepositoryRoot repository = new StateRepositoryRoot(new MemoryDictionarySource());

            uint160 cow = 100;
            uint160 horse = 2000;

            byte[] cowCode = "A1A2A3".HexToByteArray();
            byte[] horseCode = "B1B2B3".HexToByteArray();

            repository.SetCode(cow, cowCode);
            repository.SetCode(horse, horseCode);

            Assert.Equal(cowCode, repository.GetCode(cow));
            Assert.Equal(horseCode, repository.GetCode(horse));
        }

        [Fact]
        public void Test4()
        {
            MemoryDictionarySource source = new MemoryDictionarySource();
            StateRepositoryRoot root = new StateRepositoryRoot(source);

            IStateRepository repository = root.StartTracking();

            repository.SetStorageValue(new uint160(cow), cowKey, cowValue);
            repository.SetStorageValue(new uint160(horse), horseKey, horseValue);
            repository.Commit();

            Assert.Equal(cowValue, root.GetStorageValue(new uint160(cow), cowKey));
            Assert.Equal(horseValue, root.GetStorageValue(new uint160(horse), horseKey));
        }

        [Fact]
        public void Test12()
        {
            StateRepositoryRoot repository = new StateRepositoryRoot(new MemoryDictionarySource());
            IStateRepository track = repository.StartTracking();

            track.SetCode(new uint160(cow), cowCode);
            track.SetCode(new uint160(horse), horseCode);

            Assert.Equal(cowCode, track.GetCode(new uint160(cow)));
            Assert.Equal(horseCode, track.GetCode(new uint160(horse)));

            track.Rollback();

            Assert.Null(repository.GetCode(new uint160(cow)));
            Assert.Null(repository.GetCode(new uint160(horse)));
        }

        [Fact]
        public void Test20()
        {
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource());
            StateRepositoryRoot repository = new StateRepositoryRoot(stateDB);
            byte[] root = repository.Root;

            uint160 cowAddress = new uint160(cow);
            uint160 horseAddress = new uint160(horse);

            IStateRepository track2 = repository.StartTracking(); //repository
            track2.SetStorageValue(cowAddress, cowKey1, cowVal1);
            track2.SetStorageValue(horseAddress, horseKey1, horseVal1);
            track2.Commit();
            repository.Commit();

            byte[] root2 = repository.Root;

            track2 = repository.StartTracking(); //repository
            track2.SetStorageValue(cowAddress, cowKey2, cowVal0);
            track2.SetStorageValue(horseAddress, horseKey2, horseVal0);
            track2.Commit();
            repository.Commit();

            byte[] root3 = repository.Root;

            IStateRepository snapshot = new StateRepositoryRoot(stateDB, root);
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey2));

            snapshot = new StateRepositoryRoot(stateDB, root2);
            Assert.Equal(cowVal1, snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Equal(horseVal1, snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey2));

            snapshot = new StateRepositoryRoot(stateDB, root3);
            Assert.Equal(cowVal1, snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Equal(cowVal0, snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Equal(horseVal1, snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Equal(horseVal0, snapshot.GetStorageValue(horseAddress, horseKey2));
        }

        [Fact]
        public void Test20DBreeze()
        {
            DBreezeEngine engine = new DBreezeEngine(DbreezeTestLocation);
            using (DBreeze.Transactions.Transaction t = engine.GetTransaction())
            {
                t.RemoveAllKeys(DbreezeTestDb, true);
                t.Commit();
            }
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(new DBreezeByteStore(engine, DbreezeTestDb));
            StateRepositoryRoot repository = new StateRepositoryRoot(stateDB);
            byte[] root = repository.Root;

            uint160 cowAddress = new uint160(cow);
            uint160 horseAddress = new uint160(horse);

            IStateRepository track2 = repository.StartTracking(); //repository
            track2.SetStorageValue(cowAddress, cowKey1, cowVal1);
            track2.SetStorageValue(horseAddress, horseKey1, horseVal1);
            track2.Commit();
            repository.Commit();

            byte[] root2 = repository.Root;

            track2 = repository.StartTracking(); //repository
            track2.SetStorageValue(cowAddress, cowKey2, cowVal0);
            track2.SetStorageValue(horseAddress, horseKey2, horseVal0);
            track2.Commit();
            repository.Commit();

            byte[] root3 = repository.Root;

            IStateRepository snapshot = new StateRepositoryRoot(stateDB, root);
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey2));

            snapshot = new StateRepositoryRoot(stateDB, root2);
            Assert.Equal(cowVal1, snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Equal(horseVal1, snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey2));

            snapshot = new StateRepositoryRoot(stateDB, root3);
            Assert.Equal(cowVal1, snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Equal(cowVal0, snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Equal(horseVal1, snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Equal(horseVal0, snapshot.GetStorageValue(horseAddress, horseKey2));
        }

        [Fact]
        public void Repository_CommitAndRollbackTest()
        {
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource());
            StateRepositoryRoot repository = new StateRepositoryRoot(stateDB);
            IStateRepository txTrack = repository.StartTracking();
            txTrack.CreateAccount(testAddress);
            txTrack.SetStorageValue(testAddress, dog, cat);
            txTrack.Commit();
            repository.Commit();
            byte[] root1 = repository.Root;

            IStateRepository txTrack2 = repository.StartTracking();
            txTrack2.SetStorageValue(testAddress, dog, fish);
            txTrack2.Rollback();

            IStateRepository txTrack3 = repository.StartTracking();
            txTrack3.SetStorageValue(testAddress, dodecahedron, bird);
            txTrack3.Commit();
            repository.Commit();

            byte[] upToDateRoot = repository.Root;

            Assert.Equal(cat, repository.GetStorageValue(testAddress, dog));
            Assert.Equal(bird, repository.GetStorageValue(testAddress, dodecahedron));
            IStateRepository snapshot = repository.GetSnapshotTo(root1);

            repository.SyncToRoot(root1);
            Assert.Equal(cat, snapshot.GetStorageValue(testAddress, dog));
            Assert.Null(snapshot.GetStorageValue(testAddress, dodecahedron));
        }

        [Fact]
        public void Repository_CacheDoesntCarryOver()
        {
            const string testContractType = "A String";
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource());
            StateRepositoryRoot repository = new StateRepositoryRoot(stateDB);

            byte[] initialRoot = repository.Root;

            IStateRepository txTrack = repository.StartTracking();
            txTrack.CreateAccount(testAddress);
            txTrack.SetStorageValue(testAddress, dog, cat);
            txTrack.SetContractType(testAddress, testContractType);
            txTrack.Commit();
            repository.Commit();

            byte[] postChangesRoot = repository.Root;

            IStateRepositoryRoot repository2 = repository.GetSnapshotTo(initialRoot);
            Assert.Null(repository2.GetAccountState(testAddress));
            repository2.SetContractType(testAddress, "Something Else");
            repository2.SyncToRoot(postChangesRoot);
            Assert.Equal(testContractType, repository2.GetContractType(testAddress));
        }

        [Fact]
        public void Repository_CommitPushesToUnderlyingSource()
        {
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource());
            StateRepositoryRoot repository = new StateRepositoryRoot(stateDB);
            IStateRepository txTrack = repository.StartTracking();
            txTrack.CreateAccount(testAddress);
            txTrack.SetStorageValue(testAddress, dog, cat);
            Assert.Null(repository.GetStorageValue(testAddress, dog));
            txTrack.Commit();
            Assert.Equal(cat, repository.GetStorageValue(testAddress, dog));
        }

        [Fact]
        public void Repository_Bytes0VsNull()
        {
            // Demonstrates that our repository treats byte[0] and null as the same.

            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource());
            StateRepositoryRoot repository = new StateRepositoryRoot(stateDB);
            repository.CreateAccount(testAddress);
            repository.SetStorageValue(testAddress, dog, new byte[0]);
            Assert.Equal(new byte[0], repository.GetStorageValue(testAddress, dog));
            repository.Commit();

            // We have pushed byte[0] to the kv store. Should come back as byte[0] right?
            StateRepositoryRoot repository2 = new StateRepositoryRoot(stateDB, repository.Root);
            // Nope, comes back null...
            Assert.Null(repository2.GetStorageValue(testAddress, dog));
        }
    }
}