using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Tone3000;
using Tone3000.Auth;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the TONE3000 client with <see cref="IServiceCollection"/>.</summary>
public static class Tone3000ServiceCollectionExtensions
{
    /// <summary>Name of the <see cref="HttpClient"/> used for OAuth calls.</summary>
    public const string OAuthHttpClientName = "Tone3000.OAuth";

    /// <summary>
    /// Adds <see cref="ITone3000Client"/>, <see cref="Tone3000OAuthClient"/> and <see cref="Tone3000TokenManager"/>.
    /// Tokens go to an <see cref="InMemoryTokenStore"/> unless you register your own <see cref="ITokenStore"/>.
    /// Returns the API client's builder so you can add handlers such as resilience policies.
    /// </summary>
    public static IHttpClientBuilder AddTone3000Client(this IServiceCollection services, Action<Tone3000ClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<Tone3000ClientOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ITokenStore, InMemoryTokenStore>();

        services.AddHttpClient(OAuthHttpClientName);
        services.TryAddTransient(sp => new Tone3000OAuthClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(OAuthHttpClientName),
            sp.GetRequiredService<IOptions<Tone3000ClientOptions>>(),
            sp.GetRequiredService<TimeProvider>()));

        services.TryAddSingleton(sp => new Tone3000TokenManager(
            sp.GetRequiredService<ITokenStore>(),
            sp.GetRequiredService<Tone3000OAuthClient>,
            sp.GetRequiredService<IOptions<Tone3000ClientOptions>>().Value,
            sp.GetRequiredService<TimeProvider>()));

        services.TryAddTransient<Tone3000AuthHandler>();

        return services
            .AddHttpClient<ITone3000Client, Tone3000Client>((http, sp) => new Tone3000Client(
                http,
                sp.GetRequiredService<IOptions<Tone3000ClientOptions>>(),
                sp.GetRequiredService<TimeProvider>()))
            .AddHttpMessageHandler<Tone3000AuthHandler>();
    }
}
