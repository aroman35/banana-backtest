using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Options;
using Banana.Backtest.Common.Services;
using Banana.Backtest.Crypto.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class MetaBuildJob<TMarketDataType>(ICatalogue catalogue, IOptions<MarketDataParserOptions> marketDataParserOptions, ILogger logger)
    where TMarketDataType : unmanaged
{
    private readonly ILogger _logger = logger.ForContext<MetaBuildJob<TMarketDataType>>();

    public async Task HandleAsync(MarketDataHash hash)
    {
        var meta = MarketDataCacheAccessorProvider.ReadMeta<TMarketDataType>(
            marketDataParserOptions.Value.OutputDirectory, hash);
        await catalogue.BuildComplete(meta);
        _logger.Information("Meta build complete for {Hash}: {MarketData}", hash, Helpers.FriendlyTypeName<TMarketDataType>());
    }
}
