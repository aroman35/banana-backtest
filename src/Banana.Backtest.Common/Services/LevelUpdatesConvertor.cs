using System.Diagnostics;
using System.IO.Compression;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;
using Serilog;

namespace Banana.Backtest.Common.Services;

public class LevelUpdatesConvertor : IDisposable
{
    private readonly IMarketDataCacheReader<OrderUpdate> _orderUpdatesReader;
    private readonly IMarketDataCacheWriter<LevelUpdate>? _levelUpdatesWriter;
    private readonly ILogger _logger;
    private readonly Dictionary<double, MarketDataItem<LevelUpdate>> _bidLevelUpdate = new();
    private readonly Dictionary<double, MarketDataItem<LevelUpdate>> _askLevelUpdate = new();
    private readonly MarketDataHash _hash;

    private long _currentTimestamp;

    public LevelUpdatesConvertor(
        LevelUpdatesConvertorSettings settings,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.StoragePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.OutputDirectoryPath);
        _hash = settings.Hash;
        _orderUpdatesReader = MarketDataCacheAccessor.CreateReader<OrderUpdate>(settings.StoragePath, settings.Hash.For(FeedType.OrdersLog));
        if (!_orderUpdatesReader.IsEmpty)
            _levelUpdatesWriter = MarketDataCacheAccessor.CreateWriter<LevelUpdate>(settings.OutputDirectoryPath, settings.Hash.For(FeedType.LevelUpdates), CompressionType.NoCompression, CompressionLevel.NoCompression);
        _logger = logger.ForContext<LevelUpdatesConvertor>();
    }

    public void Start()
    {
        if (_orderUpdatesReader.IsEmpty)
            return;
        var startedTimestamp = Stopwatch.GetTimestamp();
        Span<MarketDataItem<OrderUpdate>> currentBatchOrders = stackalloc MarketDataItem<OrderUpdate>[2048];
        var currentBatchIterator = 0;
        
        foreach (var orderUpdate in _orderUpdatesReader.ContinueReadUntil())
        {
            var isBookUpdated = _currentTimestamp != orderUpdate.Timestamp;
            if (isBookUpdated)
            {
                HandleOrdersBatch(currentBatchOrders, currentBatchIterator);

                _currentTimestamp = orderUpdate.Timestamp;
                currentBatchIterator = 0;
            }
            currentBatchOrders[currentBatchIterator++] = orderUpdate;
        }
        HandleOrdersBatch(currentBatchOrders, currentBatchIterator);
        var jobElapsedTime = Stopwatch.GetElapsedTime(startedTimestamp);
        _logger.Information("Level updates for {Ticker}-{Date} has been built in {@JobElapsedTime} ms", _hash.Symbol.Ticker, _hash.Date, jobElapsedTime);
    }

    private void HandleOrdersBatch(Span<MarketDataItem<OrderUpdate>> currentBatchOrders, int batchSize)
    {
        for (var i = 0; i < batchSize; i++)
        {
            var order = currentBatchOrders[i];
            var updatesTable = order.Item.Side is Side.Long ? _bidLevelUpdate : _askLevelUpdate;
            updatesTable.Remove(order.Item.Price, out var levelUpdate);
            var quantity = levelUpdate.Item.Quantity + (order.Item.Type is EntryType.Place ? 1 : -1) * order.Item.Quantity;
            updatesTable[order.Item.Price] = new MarketDataItem<LevelUpdate>
            {
                Timestamp = order.Timestamp,
                Item = new LevelUpdate
                {
                    Price = order.Item.Price,
                    IsBid = order.Item.Side is Side.Long,
                    Quantity = quantity < 0 ? 0 : quantity
                }
            };
        }

        foreach (var levelUpdate in _askLevelUpdate.Values)
        {
            _levelUpdatesWriter?.Write(levelUpdate);
        }

        foreach (var levelUpdate in _bidLevelUpdate.Values)
        {
            _levelUpdatesWriter?.Write(levelUpdate);
        }
        
        _askLevelUpdate.Clear();
        _bidLevelUpdate.Clear();
    }

    public void Dispose()
    {
        _orderUpdatesReader.Dispose();
        _levelUpdatesWriter?.Dispose();
    }
}