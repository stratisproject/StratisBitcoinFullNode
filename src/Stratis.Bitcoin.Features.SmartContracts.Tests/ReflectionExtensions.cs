using System.Reflection;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public static class ReflectionExtensions
    {
        public static object GetInstancePrivateFieldValue(this object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().BaseType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field.GetValue(instance);
        }

        public static object GetPrivateFieldValue(this object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field.GetValue(obj);
        }
    }
}