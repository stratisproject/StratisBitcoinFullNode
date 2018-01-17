using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    public class NoDeleteSource<Key, Value>  : AbstractChainedSource<Key, Value, Key, Value> 
    {

    public NoDeleteSource(ISource<Key, Value> src) : base(src)
    {
        SetFlushSource(true);
    }

    public override void Delete(Key key)
    {
    }

    public override void Put(Key key, Value val)
    {
        if (val != null) GetSource().Put(key, val);
    }

    public override Value Get(Key key)
    {
        return GetSource().Get(key);
    }

    protected override bool FlushImpl()
    {
        return false;
    }
}
}
