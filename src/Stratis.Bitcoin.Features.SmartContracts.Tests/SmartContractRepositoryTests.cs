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
        private static readonly uint160 testAddress = 111111;


        [Fact]
        public void RepositoryTest()
        {
            //DBreezeEngine engine = new DBreezeEngine(@"C:\temp");
            //DBreezeByteStore byteStore = new DBreezeByteStore(engine, "kvTest");
            MemoryDictionarySource source = new MemoryDictionarySource();
            RepositoryRoot root = new RepositoryRoot(source);

            IRepository track = root.StartTracking();

            byte[] cow = StringToByteArray("CD2A3D9F938E13CD947EC05ABC7FE734DF8DD826");
            byte[] horse = StringToByteArray("13978AEE95F38490E9769C39B2773ED763D9CD5F");
            byte[] cowKey = StringToByteArray("A1A2A3");
            byte[] cowValue = StringToByteArray("A4A5A6");

            byte[] horseKey = StringToByteArray("B1B2B3");
            byte[] horseValue = StringToByteArray("B4B5B6");

            track.AddStorageRow(new uint160(cow), cowKey, cowValue);
            track.AddStorageRow(new uint160(horse), horseKey, horseValue);
            track.Commit();

            Assert.Equal(cowValue, root.GetStorageValue(new uint160(cow), cowKey));
            Assert.Equal(horseValue, root.GetStorageValue(new uint160(horse), horseKey));

            root.Close();
        }

        private static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        //[Fact]
        //public void IntegrationRepositoryTest()
        //{
        //    byte[] testCode = new byte[] { 126, 127, 128, 1, 2, 3, 4, 5 };
        //    Repository repo = new Repository();
        //    repo.Refresh();

        //    repo.CreateAccount(testAddress);
        //    repo.SetObject(testAddress, dog, cat);
        //    Assert.Equal(cat, repo.GetObject<byte[]>(testAddress, dog));

        //    repo.SetCode(testAddress, testCode);
        //    Assert.Equal(testCode, repo.GetCode(testAddress));
        //}

        //[Fact]
        //public void CommitAndRollbackTest()
        //{
        //    Repository track = new Repository();
        //    track.Refresh();

        //    IRepository txTrack = track.StartTracking();
        //    txTrack.CreateAccount(testAddress);
        //    txTrack.SetObject(testAddress, dog, cat);
        //    txTrack.Commit();
        //    byte[] rootTx1 = txTrack.GetRoot();
        //    track.LoadSnapshot(rootTx1);

        //    IRepository txTrack2 = track.StartTracking();
        //    txTrack2.SetObject(testAddress, dog, fish);
        //    txTrack2.Rollback();

        //    IRepository txTrack3 = track.StartTracking();
        //    txTrack3.SetObject(testAddress, dodecahedron, bird);
        //    txTrack3.Commit();
        //    track.LoadSnapshot(txTrack3.GetRoot());

        //    Assert.Equal(cat, track.GetObject<byte[]>(testAddress, dog));
        //    Assert.Equal(bird, track.GetObject<byte[]>(testAddress, dodecahedron));

        //    track.LoadSnapshot(rootTx1);
        //    var stuff = track.GetObject<byte[]>(testAddress, dog);
        //}

        // TODO: Test commit and rollback, building in a dictionary of not-yet-flushed tries

    }
}
