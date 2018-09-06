namespace RuntimeObserver
{
    public class Observer
    {
        private long operationCount;

        private long operationCountLimit;

        public Observer()
        {
            this.operationCountLimit = 100;
        }

        public void OperationUp()
        {
            this.operationCount += 1;
            if (this.operationCount > this.operationCountLimit)
                throw new System.Exception("too many ops");
        }


    }
}
