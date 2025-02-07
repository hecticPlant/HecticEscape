public class ContainerSZ
{
    private readonly Dictionary<Type, Func<object>> _registrations = new Dictionary<Type, Func<object>>();
    private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>(); // Dictionary für Singletons

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

    // Registrierung eines Singleton-Objekts
    public void RegisterSingleton<TService>(TService instance)
    {
        _singletons[typeof(TService)] = instance;
    }

    // Auflösung eines Typs
    public TService Resolve<TService>()
    {
        // Wenn die Instanz ein Singleton ist, gebe das Singleton zurück
        if (_singletons.ContainsKey(typeof(TService)))
        {
            return (TService)_singletons[typeof(TService)];
        }

        // Ansonsten nutze die normale Registrierung
        if (_registrations.TryGetValue(typeof(TService), out var factory))
        {
            return (TService)factory();
        }

        throw new InvalidOperationException($"Service of type {typeof(TService)} not registered.");
    }
}
