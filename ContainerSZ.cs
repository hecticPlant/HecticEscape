using System;
using System.Collections.Generic;

namespace ScreenZen
{
    public class ContainerSZ
    {
        private readonly Dictionary<Type, Func<object>> _registrations = new Dictionary<Type, Func<object>>();

        // Registrierung eines Typs mit einem Factory-Methoden-Lambda
        public void Register<TService>(Func<TService> factory)
        {
            _registrations[typeof(TService)] = () => factory();
        }

        // Registrierung eines Typs, bei dem der Typ selbst instanziiert wird
        public void Register<TService>() where TService : new()
        {
            _registrations[typeof(TService)] = () => new TService();
        }

        // Auflösung eines Typs
        public TService Resolve<TService>()
        {
            if (_registrations.TryGetValue(typeof(TService), out var factory))
            {
                return (TService)factory();
            }
            throw new InvalidOperationException($"Service of type {typeof(TService)} not registered.");
        }
    }
}
