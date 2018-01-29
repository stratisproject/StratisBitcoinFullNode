using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using DBreeze;
using Stratis.SmartContracts.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractRepositoryTests
    {
        private static readonly byte[] empty = new byte[0];
        private static readonly byte[] dog = Encoding.UTF8.GetBytes("dog");
        private static readonly byte[] dodecahedron = Encoding.UTF8.GetBytes("dodecahedron");
        private static readonly byte[] cat = Encoding.UTF8.GetBytes("cat");
        private static readonly byte[] fish = Encoding.UTF8.GetBytes("fish");
        private static readonly byte[] bird = Encoding.UTF8.GetBytes("bird");
        // EthereumJ consts below
        private static readonly byte[] cow = StringToByteArray("CD2A3D9F938E13CD947EC05ABC7FE734DF8DD826");
        private static readonly byte[] horse = StringToByteArray("13978AEE95F38490E9769C39B2773ED763D9CD5F");
        private static readonly byte[] cowCode = StringToByteArray("A1A2A3");
        private static readonly byte[] horseCode = StringToByteArray("B1B2B3");
        private static readonly byte[] cowKey = StringToByteArray("A1A2A3");
        private static readonly byte[] cowValue = StringToByteArray("A4A5A6");
        private static readonly byte[] horseKey = StringToByteArray("B1B2B3");
        private static readonly byte[] horseValue = StringToByteArray("B4B5B6");
        private static readonly byte[] cowKey1 = StringToByteArray("c1");
        private static readonly byte[] cowKey2 = StringToByteArray("c2");
        private static readonly byte[] cowVal1 = StringToByteArray("c0a1");
        private static readonly byte[] cowVal0 = StringToByteArray("c0a0");
        private static readonly byte[] horseKey1 = StringToByteArray("e1");
        private static readonly byte[] horseKey2 = StringToByteArray("e2");
        private static readonly byte[] horseVal1 = StringToByteArray("c0a1");
        private static readonly byte[] horseVal0 = StringToByteArray("c0a0");

        private static readonly uint160 testAddress = 111111;
        private const string DbreezeTestLocation = "C:/temp";
        private const string DbreezeTestDb = "test";

        // Numbered tests are taken from EthereumJ....RepositoryTests

        [Fact]
        public void Test3()
        {
            ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(new MemoryDictionarySource());

            uint160 cow = 100;
            uint160 horse = 2000;

            byte[] cowCode = StringToByteArray("A1A2A3");
            byte[] horseCode = StringToByteArray("B1B2B3");

            repository.SetCode(cow, cowCode);
            repository.SetCode(horse, horseCode);

            Assert.Equal(cowCode, repository.GetCode(cow));
            Assert.Equal(horseCode, repository.GetCode(horse));
        }


        [Fact]
        public void Test4()
        {
            MemoryDictionarySource source = new MemoryDictionarySource();
            ContractStateRepositoryRoot root = new ContractStateRepositoryRoot(source);

            IContractStateRepository repository = root.StartTracking();

            repository.SetStorageValue(new uint160(cow), cowKey, cowValue);
            repository.SetStorageValue(new uint160(horse), horseKey, horseValue);
            repository.Commit();

            Assert.Equal(cowValue, root.GetStorageValue(new uint160(cow), cowKey));
            Assert.Equal(horseValue, root.GetStorageValue(new uint160(horse), horseKey));
        }

        [Fact]
        public void Test12()
        {
            ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(new MemoryDictionarySource());
            IContractStateRepository track = repository.StartTracking();

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
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[],byte[]>(new MemoryDictionarySource());
            ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(stateDB);
            byte[] root = repository.GetRoot();

            uint160 cowAddress = new uint160(cow);
            uint160 horseAddress = new uint160(horse);

            IContractStateRepository track2 = repository.StartTracking(); //repository
            track2.SetStorageValue(cowAddress, cowKey1, cowVal1);
            track2.SetStorageValue(horseAddress, horseKey1, horseVal1);
            track2.Commit();
            repository.Commit();

            byte[] root2 = repository.GetRoot();

            track2 = repository.StartTracking(); //repository
            track2.SetStorageValue(cowAddress, cowKey2, cowVal0);
            track2.SetStorageValue(horseAddress, horseKey2, horseVal0);
            track2.Commit();
            repository.Commit();

            byte[] root3 = repository.GetRoot();

            IContractStateRepository snapshot = new ContractStateRepositoryRoot(stateDB, root);
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey2));

            snapshot = new ContractStateRepositoryRoot(stateDB, root2);
            Assert.Equal(cowVal1, snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Equal(horseVal1, snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey2));

            snapshot = new ContractStateRepositoryRoot(stateDB, root3);
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
            ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(stateDB);
            byte[] root = repository.GetRoot();

            uint160 cowAddress = new uint160(cow);
            uint160 horseAddress = new uint160(horse);

            IContractStateRepository track2 = repository.StartTracking(); //repository
            track2.SetStorageValue(cowAddress, cowKey1, cowVal1);
            track2.SetStorageValue(horseAddress, horseKey1, horseVal1);
            track2.Commit();
            repository.Commit();

            byte[] root2 = repository.GetRoot();

            track2 = repository.StartTracking(); //repository
            track2.SetStorageValue(cowAddress, cowKey2, cowVal0);
            track2.SetStorageValue(horseAddress, horseKey2, horseVal0);
            track2.Commit();
            repository.Commit();

            byte[] root3 = repository.GetRoot();

            IContractStateRepository snapshot = new ContractStateRepositoryRoot(stateDB, root);
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey2));

            snapshot = new ContractStateRepositoryRoot(stateDB, root2);
            Assert.Equal(cowVal1, snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Null(snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Equal(horseVal1, snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Null(snapshot.GetStorageValue(horseAddress, horseKey2));

            snapshot = new ContractStateRepositoryRoot(stateDB, root3);
            Assert.Equal(cowVal1, snapshot.GetStorageValue(cowAddress, cowKey1));
            Assert.Equal(cowVal0, snapshot.GetStorageValue(cowAddress, cowKey2));
            Assert.Equal(horseVal1, snapshot.GetStorageValue(horseAddress, horseKey1));
            Assert.Equal(horseVal0, snapshot.GetStorageValue(horseAddress, horseKey2));
        }

        [Fact]
        public void CommitAndRollbackTest()
        {
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource());
            ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(stateDB);
            IContractStateRepository txTrack = repository.StartTracking();
            txTrack.CreateAccount(testAddress);
            txTrack.SetStorageValue(testAddress, dog, cat);
            txTrack.Commit();
            repository.Commit();
            byte[] root1 = repository.GetRoot();

            IContractStateRepository txTrack2 = repository.StartTracking();
            txTrack2.SetStorageValue(testAddress, dog, fish);
            txTrack2.Rollback();

            IContractStateRepository txTrack3 = repository.StartTracking();
            txTrack3.SetStorageValue(testAddress, dodecahedron, bird);
            txTrack3.Commit();
            repository.Commit();

            byte[] upToDateRoot = repository.GetRoot();

            Assert.Equal(cat, repository.GetStorageValue(testAddress, dog));
            Assert.Equal(bird, repository.GetStorageValue(testAddress, dodecahedron));

            IContractStateRepository snapshot = repository.GetSnapshotTo(root1);

            repository.SyncToRoot(root1);
            Assert.Equal(cat, snapshot.GetStorageValue(testAddress, dog));
            Assert.Null(snapshot.GetStorageValue(testAddress, dodecahedron));
        }

        [Fact]
        public void CommitPushesToUnderlyingSource()
        {
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource());
            ContractStateRepositoryRoot repository = new ContractStateRepositoryRoot(stateDB);
            IContractStateRepository txTrack = repository.StartTracking();
            txTrack.CreateAccount(testAddress);
            txTrack.SetStorageValue(testAddress, dog, cat);
            Assert.Null(repository.GetStorageValue(testAddress, dog));
            txTrack.Commit();
            Assert.Equal(cat, repository.GetStorageValue(testAddress, dog));
        }

        private static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

    }
}
