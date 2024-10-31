using System.Collections.Concurrent;
using System.Reflection;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Options;
using Banana.Backtest.Common.Models.Root;
using Microsoft.Extensions.Options;
using Serilog;

namespace Banana.Backtest.Common.Services;

public class MarketDataParserHandler<TMarketDataType>(
    IOptions<MarketDataParserOptions> marketDataParserHandlerOptions,
    ILogger logger) : IParserHandler<TMarketDataType>
    where TMarketDataType : unmanaged
{
    private readonly ConcurrentDictionary<MarketDataHash, IMarketDataCacheWriter<TMarketDataType>> _openedFiles = new();
    private readonly ILogger _logger = logger.ForContext<MarketDataParserHandler<TMarketDataType>>();
    private readonly FeedType _level = typeof(TMarketDataType).GetCustomAttribute<FeedAttribute>()?.Feed
                                       ?? throw new ArgumentException($"Feed type is not defined for {typeof(TMarketDataType).Name}. Ensure that {nameof(FeedAttribute)} is set.");

    private long _counter;

    public void Handle(MarketDataItem<TMarketDataType> marketDataItem, Symbol ticker)
    {
        try
        {
            var hash = MarketDataHash.Create(ticker, marketDataItem.Date, _level);
            var prevDateFileHash = hash.SwitchDate(-1);
            if (_openedFiles.ContainsKey(prevDateFileHash))
            {
                if (_openedFiles.Remove(prevDateFileHash, out var file))
                {
                    _logger.Information("Closing file {Ticker} {Date}", hash.Symbol.ToString(), hash.Date);
                    file.Dispose();
                }
            }
            var fileOutput = GetOutFile(hash);
            fileOutput.Write(marketDataItem);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Error occured while handling level update ({Ticker}): {Data}", ticker, marketDataItem);
        }
        _counter++;
        if (_counter % 1_000_000 == 0)
            _logger.Debug("Parsed {Count} records", _counter);
    }

    public MarketDataCacheMeta Complete()
    {
        return default;
    }

    private IMarketDataCacheWriter<TMarketDataType> GetOutFile(MarketDataHash marketDataHash)
    {
        return _openedFiles.GetOrAdd(
            marketDataHash,
            hash => MarketDataCacheAccessorProvider.CreateWriter<TMarketDataType>(
                marketDataParserHandlerOptions.Value.OutputDirectory,
                hash,
                marketDataParserHandlerOptions.Value.CompressionType,
                marketDataParserHandlerOptions.Value.CompressionLevel));
    }

    public void Dispose()
    {
        _logger.Warning("Disposing market data parser handler");
        Parallel.ForEach(_openedFiles.Keys, key => _openedFiles[key].Dispose());
    }
}
