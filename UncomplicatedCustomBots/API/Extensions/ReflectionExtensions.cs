using System;
using System.Reflection;
using HarmonyLib;

namespace UncomplicatedCustomBots.API.Extensions
{
    public static class ReflectionExtensions
    {
        public static void InvokeStaticMethod(this Type type, string methodName, object[] param)
        {
            type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod)?.Invoke(null, param);
        }

        public static void InvokeStaticEvent(this Type type, string eventName, object[] param)
        {
            MulticastDelegate multicastDelegate = (MulticastDelegate)type.GetField(eventName, AccessTools.all).GetValue(null);
            if ((object)multicastDelegate != null)
            {
                Delegate[] invocationList = multicastDelegate.GetInvocationList();
                foreach (Delegate obj in invocationList)
                {
                    obj.Method.Invoke(obj.Target, param);
                }
            }
        }
    }
}