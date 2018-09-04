using System;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// Specifies that the value of a field can be used to search for receipts.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class IndexAttribute : Attribute
    {

    }
}
