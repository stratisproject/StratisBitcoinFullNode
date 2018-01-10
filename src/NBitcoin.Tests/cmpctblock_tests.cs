namespace NBitcoin.Tests
{
    public class cmpctblock_tests
    {
        public cmpctblock_tests()
        {
            // These flags may get set due to static network initializers
            // which include the initializers for Stratis.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }
                /*
         * TODO: Consider importing to FN.

        [Fact]
        [Trait("CoreBeta", "CoreBeta")]
        public void CanRoundtripCmpctBlock()
        {
            Block block = new Block();
            block.Transactions.Add(new Transaction());
            var cmpct = new CmpctBlockPayload(block);
            cmpct.Clone();
        }

*/    }
}
