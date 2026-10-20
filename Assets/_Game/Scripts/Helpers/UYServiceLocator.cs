using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unyawn.Utils
{
    public class UYServiceLocator
    {
        private static UYServiceLocator _instance;
        private readonly Dictionary<Type, object> _services = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainLoad()
        {
            _instance = null;
        }


        public UYServiceLocator()
        {
            if (_instance == null)
                _instance = this;
        }


        /// <summary>
        /// Getter for singelton instance.
        /// </summary>
        protected static UYServiceLocator Instance
        {
            get
            {
                if (_instance == null)
                {
                    new UYServiceLocator();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Register the specified service instance. Usually called in Awake(), like this:
        /// </summary>
        /// <param name="service">Service instance object.</param>
        /// <typeparam name="T">Type of the instance object.</typeparam>
        public static void Register<T>(T service, bool overwrite = true) where T : class
        {
            if (service == null)
                return;

            if (!overwrite && Instance._services.ContainsKey(typeof(T)))
                return;

            Instance._services[typeof(T)] = service;
        }

        public static bool Has<T>() where T : class
        {
            if (_instance == null || !Instance._services.TryGetValue(typeof(T), out var raw))
                return false;

            if (raw is UnityEngine.Object unityObject && unityObject == null)
            {
                Instance._services.Remove(typeof(T));
                return false;
            }

            return raw != null;
        }


        /// <summary>
        /// Find the instance of the specified service. Usually called in Start(), like this:
        /// ExampleService es = Get<ExampleService>();
        /// Throws new UnityException if service not found.
        /// </summary>
        /// <typeparam name="T">Type of the service.</typeparam>
        /// <returns>Service instance, or null if not initialized</returns>
        public static T Get<T>() where T : class
        {
            if (!Has<T>())
                return null;

            return Instance._services[typeof(T)] as T;
        }

        public static void Unregister<T>()
        {
            if (Instance._services.ContainsKey(typeof(T)))
            {
                Instance._services.Remove(typeof(T));
            }
        }


        /// <summary>
        /// Clears internal dictionary of service instances.
        /// This will not clear out any global state that they contain,
        /// unless there are no other references to the object.
        /// </summary>
        public static void Clear()
        {
            Instance._services.Clear();
        }

    }
}