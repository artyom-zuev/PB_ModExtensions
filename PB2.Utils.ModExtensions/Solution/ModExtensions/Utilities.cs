using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ModExtensions
{
    public static class ModUtilities
    {
        private static Dictionary<Type, Dictionary<string, FieldInfo>> fieldsPrivatePerType = new Dictionary<Type, Dictionary<string, FieldInfo>> ();
        private static Dictionary<Type, Dictionary<string, MethodInfo>> methodsPrivatePerType = new Dictionary<Type, Dictionary<string, MethodInfo>> ();
        private static StringBuilder sb = new StringBuilder ();

        public static FieldInfo GetPrivateFieldInfo (object instance, string fieldName, bool isStatic, bool throwOnError = false)
        {
            if (instance == null)
                return null;

            var instanceType = instance.GetType ();
            return GetPrivateFieldInfo (instanceType, fieldName, isStatic, throwOnError);
        }
        
        public static FieldInfo GetPrivateFieldInfo (Type type, string fieldName, bool isStatic, bool throwOnError = false)
        {
            if (type == null)
                return null;
            
            Dictionary<string, FieldInfo> fieldsPrivate = null;
            if (!fieldsPrivatePerType.TryGetValue (type, out fieldsPrivate))
            {
                fieldsPrivate = new Dictionary<string, FieldInfo> ();
                fieldsPrivatePerType[type] = fieldsPrivate;
            }

            FieldInfo fieldInfo = null;
            if (!fieldsPrivate.TryGetValue (fieldName, out fieldInfo))
            {
                // Use BindingFlags.NonPublic | BindingFlags.Instance to find a private instance field
                var binding = BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                fieldInfo = type.GetField (fieldName, binding);
                if (fieldInfo != null)
                    fieldsPrivate[fieldName] = fieldInfo;
                else
                {
                    var message = $"ModExtensions | Type {type.GetNiceTypeName ()} field {fieldName} ({binding}) not found";
                    if (throwOnError)
                        throw new Exception (message);
                    
                    Debug.LogWarning (message);
                    return null;
                }
            }

            return fieldInfo;
        }
        
        public static T GetFieldInfoValue<T> (this FieldInfo fieldInfo, object instance)
        {
            if (fieldInfo == null)
                return default;
            
            var valueObj = fieldInfo.GetValue (instance);
            try
            {
                return (T)valueObj;
            }
            catch (InvalidCastException)
            {
                Debug.LogWarning ($"ModExtensions | Field {fieldInfo.Name} has type {fieldInfo.FieldType.GetNiceTypeName ()}, value can't be cast to type {typeof(T).GetNiceTypeName ()}");
                return default (T);
            }
        }
        
        
        
        public static MethodInfo GetPrivateMethodInfo (object instance, string methodName, bool isStatic, bool throwOnError = false)
        {
            if (instance == null)
                return null;

            var instanceType = instance.GetType ();
            return GetPrivateMethodInfo (instanceType, methodName, isStatic, throwOnError);
        }
        
        public static MethodInfo GetPrivateMethodInfo (Type type, string methodName, bool isStatic, bool throwOnError = false)
        {
            if (type == null)
                return null;
            
            Dictionary<string, MethodInfo> methodsPrivate = null;
            if (!methodsPrivatePerType.TryGetValue (type, out methodsPrivate))
            {
                methodsPrivate = new Dictionary<string, MethodInfo> ();
                methodsPrivatePerType[type] = methodsPrivate;
            }

            MethodInfo methodInfo = null;
            if (!methodsPrivate.TryGetValue (methodName, out methodInfo))
            {
                // Use BindingFlags.NonPublic | BindingFlags.Instance to find a private instance field
                var binding = BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                methodInfo = type.GetMethod (methodName, binding);
                if (methodInfo != null)
                    methodsPrivate[methodName] = methodInfo;
                else
                {
                    var message = $"ModExtensions | Type {type.GetNiceTypeName ()} method {methodName} ({binding}) not found";
                    if (throwOnError)
                        throw new Exception (message);
                    
                    Debug.LogWarning (message);
                    return null;
                }
            }

            return methodInfo;
        }
        
        public static T GetMethodInfoOutput<T> (this MethodInfo methodInfo, object instance, object[] parameters = null)
        {
            if (methodInfo == null)
                return default;
            
            var valueObj = methodInfo.Invoke (instance, parameters);
            if (valueObj is T)
            {
                return (T)valueObj;
            }
            
            Debug.LogWarning ($"ModExtensions | Method {methodInfo.Name} with return type {methodInfo.ReturnType.GetNiceTypeName ()} can't return a value of type {typeof(T).GetNiceTypeName ()}");
            return default;
        }

        public static Action GetActionFromMethodInfo (this MethodInfo methodInfo, object instance)
        {
            if (methodInfo == null)
                return null;
            
            var instanceAction = (Action)Delegate.CreateDelegate(typeof(Action), instance, methodInfo);
            return instanceAction;
        }

        public static Action<T> GetActionFromMethodInfo<T> (this MethodInfo methodInfo, object instance)
        {
            if (methodInfo == null)
                return null;
            
            var instanceAction = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), instance, methodInfo);
            return instanceAction;
        }
        
        public static Action<T1, T2> GetActionFromMethodInfo<T1, T2> (this MethodInfo methodInfo, object instance)
        {
            if (methodInfo == null)
                return null;
            
            var instanceAction = (Action<T1, T2>)Delegate.CreateDelegate(typeof(Action<T1, T2>), instance, methodInfo);
            return instanceAction;
        }
        
        public static Action<T1, T2, T3> GetActionFromMethodInfo<T1, T2, T3> (this MethodInfo methodInfo, object instance)
        {
            if (methodInfo == null)
                return null;
            
            var instanceAction = (Action<T1, T2, T3>)Delegate.CreateDelegate(typeof(Action<T1, T2, T3>), instance, methodInfo);
            return instanceAction;
        }
    }
}