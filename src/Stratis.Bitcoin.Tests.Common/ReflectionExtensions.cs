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

        /// <summary>Gets private constant member of specified type.</summary>
        public static T GetPrivateConstantValue<T>(this Type type, string constantName)
        {
            T value = type
                .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(T) && fi.Name == constantName)
                .Select(x => (T)x.GetRawConstantValue())
                .First();

            return value;
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
            propertyInfos = propertyInfos.Where(i => !ReferenceEquals(i, null)).ToList();
            if (propertyInfos.Count != 0)
                return propertyInfos[0];

            var fieldInfos = new List<FieldInfo>();

            fieldInfos.Add(obj.GetType().GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy));

            // To add more types of properties.
            fieldInfos = fieldInfos.Where(i => !ReferenceEquals(i, null)).ToList();

            if (fieldInfos.Count != 0)
                return fieldInfos[0];

            return null;
        }

        /// <summary>Calls private method using reflection.</summary>
        public static object InvokeMethod<T>(this T obj, string methodName, params object[] args)
        {
            Type type = typeof(T);
            MethodInfo method = type.GetTypeInfo().GetDeclaredMethod(methodName);
            return method.Invoke(obj, args);
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
        /// Sets a private variable value for a given object.
        /// </summary>
        /// <typeparam name="T">Type of the variable.</typeparam>
        /// <param name="obj">Object from where the variable value is set</param>
        /// <param name="variableName">Variable name as string.</param>
        /// <param name="value">Value to set.</param>
        public static void SetPrivateVariableValue<T>(this object obj, string variableName, T value)
        {
            FieldInfo variable = obj.GetType().GetField(variableName, BindingFlags.NonPublic| BindingFlags.Instance);
            variable.SetValue(obj, value);
        }

        [System.Diagnostics.DebuggerHidden]
        private static T As<T>(this object obj)
        {
            return (T)obj;
        }
    }
}
