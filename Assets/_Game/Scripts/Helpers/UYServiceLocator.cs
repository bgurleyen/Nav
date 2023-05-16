using System;
using System.Collections.Generic;

namespace Unyawn.Utils
{
    public class UYServiceLocator
    {
        public class CannotHaveTwoInstancesException : Exception
        {
            public CannotHaveTwoInstancesException() : base(
                "There can be only one instance of a the Services class. It is a singleton...")
            {
            }
        }

        public class ServiceAlreadyRegisteredException : Exception
        {
            public ServiceAlreadyRegisteredException(Type T) : base(
                $"A service of that name ({T})  has already been registered!")
            {
            }
        }

        public class ServiceNotFoundException : Exception
        {
            public ServiceNotFoundException(Type T) : base(
                $"Service ({T}) not found.\nAlways Register() in Awake(). Never Find() in Awake(). Check Script Execution Order.")
            {
            }
        }


        private static UYServiceLocator _instance;
        private readonly Dictionary<Type, object> _services = new();


        public UYServiceLocator()
        {
            if (_instance != null)
            {
                throw new CannotHaveTwoInstancesException();
            }

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
        public static void Register<T>(T service, bool overwrite = false) where T : class
        {
            if (!overwrite && Instance._services.ContainsKey(typeof(T)))
            {
                throw new ServiceAlreadyRegisteredException(typeof(T));
            }

            Instance._services[typeof(T)] = service;
        }

        public static bool Has<T>() where T : class
        {
            return Instance._services.ContainsKey(typeof(T));
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
            var ret = Instance._services[typeof(T)] as T;

            return ret;
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


        /// <summary>
        /// Prints the list of services in the debug console
        /// </summary>
        public static void Debug()
        {
            string output = "Debug Services List:\n";

            foreach (var s in Instance._services)
            {
                output += "* " + s.Key + " = " + s.Value.ToString() + "\n";
            }

            output += "Total: " + Instance._services.Count.ToString() + " services registered.";

            UnityEngine.Debug.Log(output);
        }

    }
}