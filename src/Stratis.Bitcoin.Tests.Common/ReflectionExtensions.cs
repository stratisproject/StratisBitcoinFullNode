using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stratis.Bitcoin.Tests.Common
{
    /// <summary>Extension methods for using reflection to get / set member values.</summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Gets the public or private member using reflection.
        /// </summary>
        /// <param name="obj">The source target.</param>
        /// <param name="memberName">Name of the field or property.</param>
        /// <returns>the value of member</returns>
        public static object GetMemberValue(this object obj, string memberName)
        {
            MemberInfo memberInfo = GetMemberInfo(obj, memberName);

            if (memberInfo == null)
                throw new Exception("memberName");

            if (memberInfo is PropertyInfo)
                return memberInfo.As<PropertyInfo>().GetValue(obj, null);

            if (memberInfo is FieldInfo)
                return memberInfo.As<FieldInfo>().GetValue(obj);

            throw new Exception();
        }

        /// <summary>
        /// Gets the member info.
        /// </summary>
        /// <param name="obj">Source object.</param>
        /// <param name="memberName">Name of member.</param>
        /// <returns>Instantiate of MemberInfo corresponding to member.</returns>
        private static MemberInfo GetMemberInfo(object obj, string memberName)
        {
            var propertyInfos = new List<PropertyInfo>();

            propertyInfos.Add(obj.GetType().GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy));
            propertyInfos = Enumerable.ToList(Enumerable.Where(propertyInfos, i => !ReferenceEquals(i, null)));
            if (propertyInfos.Count != 0)
                return propertyInfos[0];

            var fieldInfos = new List<FieldInfo>();

            fieldInfos.Add(obj.GetType().GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy));

            // To add more types of properties.
            fieldInfos = Enumerable.ToList(Enumerable.Where(fieldInfos, i => !ReferenceEquals(i, null)));

            if (fieldInfos.Count != 0)
                return fieldInfos[0];

            return null;
        }

        /// <summary>
        /// Sets a private property value for a given object.
        /// </summary>
        /// <typeparam name="T">Type of the Property</typeparam>
        /// <param name="obj">Object from where the Property Value is set</param>
        /// <param name="propertyName">Property name as string.</param>
        /// <param name="value">Value to set.</param>
        /// <returns>PropertyValue</returns>
        public static void SetPrivatePropertyValue<T>(this object obj, string propertyName, T value)
        {
            Type type = obj.GetType();

            if (type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) == null)
                throw new ArgumentOutOfRangeException("propertyName", string.Format("Property {0} was not found in Type {1}", propertyName, obj.GetType().FullName));

            type.InvokeMember(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, obj, new object[] { value });
        }

        [System.Diagnostics.DebuggerHidden]
        private static T As<T>(this object obj)
        {
            return (T)obj;
        }
    }
}
