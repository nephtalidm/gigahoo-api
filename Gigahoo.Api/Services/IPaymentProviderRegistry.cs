namespace Gigahoo.Api.Services;

// Holds every registered IPaymentProvider at once (not a single config-selected
// one) so the app can support multiple payment providers active simultaneously.
// New payments use Default; existing customers are resolved by their stored
// PaymentCustomer.Provider via Get(name).
public interface IPaymentProviderRegistry
{
    // Resolve a provider by its IPaymentProvider.Name. Throws if unknown.
    IPaymentProvider Get(string name);

    // The provider used for NEW payments (config "Payments:DefaultProvider",
    // fallback "stripe").
    IPaymentProvider Default { get; }

    // Names of all registered providers.
    IReadOnlyList<string> Names { get; }
}

// Backed by the DI-registered IEnumerable<IPaymentProvider>: every
// AddScoped<IPaymentProvider, XProvider>() automatically shows up here.
public class PaymentProviderRegistry : IPaymentProviderRegistry
{
    private readonly Dictionary<string, IPaymentProvider> _providers;
    private readonly IPaymentProvider _default;

    public PaymentProviderRegistry(IEnumerable<IPaymentProvider> providers, IConfiguration config)
    {
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        if (_providers.Count == 0)
            throw new InvalidOperationException("No IPaymentProvider implementations are registered.");

        var defaultName = config["Payments:DefaultProvider"] ?? "stripe";
        if (!_providers.TryGetValue(defaultName, out var def))
            throw new InvalidOperationException(
                $"Default payment provider '{defaultName}' (Payments:DefaultProvider) is not registered. " +
                $"Registered providers: {string.Join(", ", _providers.Keys)}.");
        _default = def;
    }

    public IPaymentProvider Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_providers.TryGetValue(name, out var provider))
            throw new InvalidOperationException(
                $"Unknown payment provider '{name}'. Registered providers: {string.Join(", ", _providers.Keys)}.");
        return provider;
    }

    public IPaymentProvider Default => _default;

    public IReadOnlyList<string> Names => _providers.Values.Select(p => p.Name).ToList();
}
