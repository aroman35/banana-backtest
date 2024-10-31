namespace Banana.Backtest.CryptoConverter.Extensions;

public static class OptionsExtensions
{
    /// <summary>
    /// Add a typed options to App configuration using the TOptions type name
    /// </summary>
    public static IServiceCollection ConfigureOptions<TOptions>(this IServiceCollection serviceCollection, IConfiguration configuration, string? name = null)
        where TOptions : class, new()
    {
        var section = configuration.GetSection(string.IsNullOrEmpty(name) ? typeof(TOptions).Name : name);
        return serviceCollection.Configure<TOptions>(section);
    }

    /// <summary>
    /// Access typed options from app configuration
    /// </summary>
    public static TOptions GetOptions<TOptions>(this IConfiguration configuration)
        where TOptions : class, new()
    {
        var options = configuration.GetSection(typeof(TOptions).Name).Get<TOptions>()
                      ?? throw new ArgumentNullException(typeof(TOptions).Name, $"Options of type {typeof(TOptions).Name} were not found");
        return options;
    }
}