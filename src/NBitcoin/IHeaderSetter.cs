using System.Threading.Tasks;

namespace NBitcoin
{
    /// <summary>
    /// This interface allow to set a header.
    /// Forcing an header could be risky and lead to race conditions.
    /// To prevent misuse, classes implementing this interface should implement the methods explicitly.
    /// <see href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/interfaces/explicit-interface-implementation"/>
    /// </summary>
    public interface IHeaderSetter
    {
        void SetHeader(BlockHeader newHeader);
    }
}
