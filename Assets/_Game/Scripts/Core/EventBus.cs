using System;
using System.Collections.Generic;
using UnityEngine;

namespace NG.Core
{
    /// <summary>
    /// Enkel typad händelsebuss. Logik publicerar, vyer prenumererar.
    /// Händelser är små structs (se GameEvents.cs) för att undvika allokeringar.
    /// </summary>
    public static class EventBus
    {
        static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            Handlers.TryGetValue(typeof(T), out var existing);
            Handlers[typeof(T)] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (!Handlers.TryGetValue(typeof(T), out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) Handlers.Remove(typeof(T));
            else Handlers[typeof(T)] = remaining;
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (!Handlers.TryGetValue(typeof(T), out var d)) return;
            foreach (var h in d.GetInvocationList())
            {
                try { ((Action<T>)h)(evt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnDomainReload() => Handlers.Clear();
    }
}
