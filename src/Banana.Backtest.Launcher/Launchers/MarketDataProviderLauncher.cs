using Banana.Backtest.Emulator.Abstractions;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.Services;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.Launcher.Launchers;

public class MarketDataProviderLauncher(
    IChannelsProvider channelsProvider,
    IOptions<MarketDataSettings> settings,
    ILogger logger)
    : MarketDataProvider(channelsProvider, settings, logger), IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return ExecuteStreamingAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Complete(cancellationToken);
    }
}
