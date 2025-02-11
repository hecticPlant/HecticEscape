public class ContainerSZ
{
    private readonly Dictionary<Type, Func<object>> _registrations = new Dictionary<Type, Func<object>>();
    private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();

    // Registrierung eines Typs mit einer Factory-Methode (optional als Singleton)
    public void Register<TService>(Func<TService> factory, bool asSingleton = false)
    {
        if (asSingleton)
        {
            var lazyInstance = new Lazy<object>(() => factory());
            _singletons[typeof(TService)] = lazyInstance.Value;
        }
        else
        {
            _registrations[typeof(TService)] = () => factory();
        }
    }

    // Registrierung eines Typs, der selbst instanziiert wird (optional als Singleton)
    public void Register<TService>(bool asSingleton = false) where TService : new()
    {
        Register(() => new TService(), asSingleton);
    }

    // Registrierung eines expliziten Singleton-Objekts
    public void RegisterSingleton<TService>(TService instance)
    {
        _singletons[typeof(TService)] = instance;
    }

    // Auflösung eines Typs
    public TService Resolve<TService>()
    {
        Type serviceType = typeof(TService);

        if (_singletons.ContainsKey(serviceType))
        {
            return (TService)_singletons[serviceType];
        }

        if (_registrations.TryGetValue(serviceType, out var factory))
        {
            return (TService)factory();
        }

        throw new InvalidOperationException($"Service {serviceType.Name} wurde nicht registriert.");
    }
}
