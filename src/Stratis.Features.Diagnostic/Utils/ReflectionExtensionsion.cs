using System;
using System.Reflection;

namespace Stratis.Features.Diagnostic.Utils
{
    internal static class ReflectionExtension
    {
        /// <summary>
        /// Gets the private property value.
        /// </summary>
        /// <typeparam name="T">Type of the Property</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>The property value.</returns>
        public static T GetPrivatePropertyValue<T>(this object obj, string propertyName)
        {
            Type type = obj.GetType();

            if (type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) == null)
                throw new ArgumentOutOfRangeException("propertyName", string.Format("Property {0} was not found in Type {1}", propertyName, obj.GetType().FullName));

            return (T)type.InvokeMember(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetProperty | BindingFlags.Instance, null, obj, null);
        }

        /// <summary>
        /// Sets a private property value for a given object.
        /// </summary>
        /// <typeparam name="T">Type of the Property</typeparam>
        /// <param name="obj">Object from where the Property Value is set</param>
        /// <param name="propertyName">Property name as string.</param>
        /// <param name="value">Value to set.</param>
        public static void SetPrivatePropertyValue<T>(this object obj, string propertyName, T value)
        {
            Type type = obj.GetType();

            if (type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) == null)
                throw new ArgumentOutOfRangeException("propertyName", string.Format("Property {0} was not found in Type {1}", propertyName, obj.GetType().FullName));

            type.InvokeMember(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, obj, new object[] { value });
        }

        /// <summary>
        /// Using reflection, retrieves the value of private field with this name on the supplied object. If no field is found, returns null.
        /// </summary>
        /// <typeparam name="T">Type of the Property</typeparam>
        /// <param name="obj">Object from where the Property Value is set</param>
        /// <param name="fieldName">Name of the field.</param>
        /// <returns>The field value</returns>
        public static T GetPrivateFieldValue<T>(this object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field?.GetValue(obj);
        }
    }
}
