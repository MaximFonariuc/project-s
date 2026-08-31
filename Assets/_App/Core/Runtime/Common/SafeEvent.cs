using System;
using UnityEngine;

namespace _App
{
    internal static class SafeEvent
    {
        public static void Publish<T>(
            Action<T> handlers,
            T value,
            UnityEngine.Object context)
        {
            if (handlers == null)
            {
                return;
            }

            Delegate[] subscriptions = handlers.GetInvocationList();
            for (int i = 0; i < subscriptions.Length; i++)
            {
                try
                {
                    ((Action<T>)subscriptions[i]).Invoke(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, context);
                }
            }
        }

        public static void Publish(Action handlers, UnityEngine.Object context)
        {
            if (handlers == null)
            {
                return;
            }

            Delegate[] subscriptions = handlers.GetInvocationList();
            for (int i = 0; i < subscriptions.Length; i++)
            {
                try
                {
                    ((Action)subscriptions[i]).Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, context);
                }
            }
        }
    }
}
