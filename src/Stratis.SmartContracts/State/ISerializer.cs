using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public interface ISerializer<T, S>
    {
        S Serialize(T obj);
        T Deserialize(S stream);
    }
}
