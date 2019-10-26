using System.Reflection;

namespace Stratis.SmartContracts.CLR.Tests
{
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Using reflection, retrieves the value of private field with this name on the supplied object's base type. If no field is found, returns null.
        /// </summary>
        public static object GetBaseTypePrivateFieldValue(this object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().BaseType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(obj);
        }

        /// <summary>
        /// Using reflection, retrieves the value of private field with this name on the supplied object. If no field is found, returns null.
        /// </summary>
        public static object GetPrivateFieldValue(this object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(obj);
        }

        /// <summary>
        /// Using reflection, sets the value of private field with this name on the supplied object. If no field is found, returns null.
        /// </summary>
        public static void SetPrivateFieldValue(this object obj, string fieldName, object value)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}