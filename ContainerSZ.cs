using System;
using System.Collections.Generic;

namespace ScreenZen
{
    public class ContainerSZ
    {
        private readonly Dictionary<Type, object> _singletons = new();
        private readonly Dictionary<Type, Func<object>> _registrations = new();

        public void RegisterSingleton<T>(T instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance), "Die Instanz darf nicht null sein.");
            
            Logger.Instance.Log($"RegisterSingleton: {typeof(T).Name}", LogLevel.Debug);
            _singletons[typeof(T)] = instance;
        }

        public void Register<T>(Func<T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Die Factory-Funktion darf nicht null sein.");
            
            Logger.Instance.Log($"Register Factory: {typeof(T).Name}", LogLevel.Debug);
            _registrations[typeof(T)] = () => factory()!;
        }

        public T Resolve<T>()
        {
            Logger.Instance.Log($"Resolve: {typeof(T).Name}", LogLevel.Debug);
            if (_singletons.TryGetValue(typeof(T), out var singleton))
                return (T)singleton;
            if (_registrations.TryGetValue(typeof(T), out var factory))
                return (T)factory();
            throw new InvalidOperationException($"Typ {typeof(T).Name} nicht registriert.");
        }
    }
}
