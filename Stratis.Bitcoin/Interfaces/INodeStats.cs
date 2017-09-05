using System.Text;

namespace Stratis.Bitcoin.Interfaces
{
    public interface INodeStats
    {
        void AddNodeStats(StringBuilder benchLog);
    }
}
