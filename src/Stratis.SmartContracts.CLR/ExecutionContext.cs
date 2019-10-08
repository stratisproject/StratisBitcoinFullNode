using System;

namespace Stratis.SmartContracts.CLR
{
    public class ExecutionContext
    {
        public ExecutionContext()
        {
            this.Id = Guid.NewGuid().ToString();
        }

        public string Id { get; }
    }
}