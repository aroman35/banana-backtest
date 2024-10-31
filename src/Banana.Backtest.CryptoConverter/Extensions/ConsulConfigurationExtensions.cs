using Consul;
using Serilog;
using Serilog.Events;
using Winton.Extensions.Configuration.Consul;

namespace Banana.Backtest.CryptoConverter.Extensions;

public static class ConsulConfigurationExtensions
{
    public const string ENV_CONSUL_URI = "CONSUL_URI";
    public const string ENV_CONSUL_PATH = "CONSUL_PATH";
    public const string ENV_CONSUL_ENABLED = "CONSUL_ENABLED";
    public const string ENV_CONSUL_TOKEN = "CONSUL_TOKEN";
    public const string ENV_CONSUL_APP_NAME = "CONSUL_APP_NAME";

    /// <summary>
    /// Add the configuration from the provider based on app env.
    /// If env:CONSUL_ENABLED=True it will try to find the source settings in Consul hosting at env:CONSUL_URI using env:CONSUL_PATH as the root directory.
    /// Otherwise,settings source will be loaded as a json file located at the ContentRoot dir
    /// </summary>
    /// <param name="configurationBuilder">Configurable builder</param>
    /// <param name="consulSettingsName">File name in consul (without file extension)</param>
    /// <param name="configuration">Host root configuration with configured env</param>
    /// <param name="isOptional">When true it will not throw if the source not found</param>
    /// <param name="localFileName">Alternative name for local file (with file extension)</param>
    /// <param name="isRootSettings">Settings are in a root dir</param>
    /// <returns></returns>
    /// <exception cref="ConsulConfigurationException"></exception>
    public static IConfigurationBuilder AddServiceConfiguration(
        this IConfigurationBuilder configurationBuilder,
        string consulSettingsName,
        IConfiguration configuration,
        bool isOptional = true,
        string? localFileName = null,
        bool isRootSettings = false)
    {
        var consulPath = configuration.GetValue<string>(ENV_CONSUL_PATH);
        var consulUri = configuration.GetValue<string>(ENV_CONSUL_URI);
        var consulAppName = configuration.GetValue<string>(ENV_CONSUL_APP_NAME);
        var fileNameWithExtension = consulSettingsName + ".json";
        var settingsName = isRootSettings ? fileNameWithExtension : $"{consulAppName}/{fileNameWithExtension}";
        if (string.IsNullOrEmpty(localFileName))
            localFileName = fileNameWithExtension;

        if (!configuration.GetValue<bool>(ENV_CONSUL_ENABLED))
        {
            configurationBuilder.AttachSettingsFromFile(settingsName, isOptional, localFileName);
            return configurationBuilder;
        }

        if (string.IsNullOrEmpty(consulPath) || string.IsNullOrEmpty(consulUri))
        {
            if (!isOptional)
                throw new InvalidOperationException("No consul path or url was provided");
            configurationBuilder.AttachSettingsFromFile(settingsName, true, localFileName);
            return configurationBuilder;
        }

        var key = CombinePath(consulPath, settingsName);
        try
        {
            configurationBuilder.AddConsul(
                key,
                consulConfiguration =>
                {
                    consulConfiguration.ConsulConfigurationOptions = options =>
                    {
                        options.Address = new Uri(consulUri);
                        var token = configuration.GetValue<string>(ENV_CONSUL_TOKEN);
                        if (!string.IsNullOrEmpty(token))
                            options.Token = token;
                    };
                    consulConfiguration.Optional = isOptional;
                    consulConfiguration.ReloadOnChange = true;
                    consulConfiguration.OnLoadException = exceptionContext => { exceptionContext.Ignore = false; };
                    consulConfiguration.ConsulHttpClientOptions = x => x.Timeout = TimeSpan.FromSeconds(5);
                });
        }
        catch (Exception exception)
        {
            Log.Write(LogEventLevel.Error, "Unable to attach settings from Consul at path {Path}: {ErrorMessage}", key, exception.Message);
            configurationBuilder.AttachSettingsFromFile(settingsName, isOptional, localFileName);
        }
        return configurationBuilder;
    }

    private static string CombinePath(params string[] pathParts)
    {
        return Path.Combine(pathParts).Replace('\\', '/');
    }

    private static void AttachSettingsFromFile(
        this IConfigurationBuilder configurationBuilder,
        string settingsName,
        bool isOptional = true,
        string? localFileName = null)
    {
        var loadedConfigurationFile = localFileName ?? settingsName;
        configurationBuilder.AddJsonFile(loadedConfigurationFile, isOptional);
        Log.Write(LogEventLevel.Information, "Attached settings from JSON file {FileName} [{IsOptional}]", loadedConfigurationFile, isOptional ? "Optional" : "Required");
    }
}