using System.IO.Compression;
using System.Reflection;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;

namespace Banana.Backtest.Common.Services;

public unsafe class MarketDataCacheAccessor<TMarketDataType> : IMarketDataCacheWriter<TMarketDataType>, IMarketDataCacheReader<TMarketDataType>
    where TMarketDataType : unmanaged
{
    private readonly FeedType _feed = typeof(TMarketDataType).GetCustomAttribute<FeedAttribute>()?.Feed
                                          ?? throw new ArgumentException(
                                              $"Feed type is not defined for {typeof(TMarketDataType).Name}. Ensure that {nameof(FeedAttribute)} is set.");
    private readonly FileStream? _sourceFileStream;
    private readonly MarketDataHash _hash;
    private readonly CompressionType _compressionType;
    private readonly CompressionLevel _compressionLevel;
    private readonly bool _isReader;
    private readonly bool _isEmpty;
    private Stream? _compressionStream;
    private long _itemsCount;
    private long _readerPosition;
    private volatile bool _disposed;
    private MarketDataItem<TMarketDataType> _next;

    // Constructor for write operations
    internal MarketDataCacheAccessor(
        string? sourcesDirectory,
        MarketDataHash hash,
        CompressionType compressionType,
        CompressionLevel compressionLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcesDirectory);

        _isReader = false;
        _hash = hash.For(_feed);
        _compressionType = compressionType;
        _compressionLevel = compressionLevel;
        var filePath = hash.FilePath(sourcesDirectory);

        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            BufferSize = 1024 * sizeof(TMarketDataType),
            Mode = FileMode.CreateNew,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan
        };

        _sourceFileStream = new FileStream(filePath, options);
        _sourceFileStream.Seek(sizeof(MarketDataCacheMeta), SeekOrigin.Begin);

        _compressionStream = compressionType switch
        {
            CompressionType.NoCompression => _sourceFileStream,
            CompressionType.Brotli => new BrotliStream(_sourceFileStream, compressionLevel, true),
            CompressionType.Deflate => new DeflateStream(_sourceFileStream, compressionLevel, true),
            CompressionType.GZip => new GZipStream(_sourceFileStream, compressionLevel, true),
            _ => _sourceFileStream
        };
    }

    // Constructor for read
    internal MarketDataCacheAccessor(string? sourcesDirectory, MarketDataHash hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcesDirectory);

        _isReader = true;
        _hash = hash.For(_feed);
        var filePath = hash.FilePath(sourcesDirectory);
        if (!File.Exists(filePath))
        {
            _isEmpty = true;
            return;
        }

        var options = new FileStreamOptions
        {
            Access = FileAccess.Read,
            BufferSize = 1024 * sizeof(TMarketDataType),
            Mode = FileMode.Open,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan
        };
        _sourceFileStream = File.Open(filePath, options);
        var meta = ExtractMeta();
        if (meta.Hash.Feed != _feed)
            throw new ArgumentException($"Invalid market data file. Expected {_feed}. Found {meta.Hash.Feed}.");
        if (meta.Hash.Symbol != hash.Symbol)
            throw new AggregateException(
                $"Invalid market data file. Expected {hash.Symbol.Ticker}. Found {meta.Hash.Symbol.Ticker}.");

        _compressionType = meta.CompressionType;
        _compressionLevel = meta.CompressionLevel;
        _itemsCount = meta.ItemsCount;

        ResetReader();
    }

    public bool IsEmpty => _isEmpty;
    public long ItemsCount => _itemsCount;
    public MarketDataHash Hash => _hash;

    public void Write(MarketDataItem<TMarketDataType> marketDataItem)
    {
        WriteInternal(_compressionStream!, marketDataItem);
        _itemsCount++;
    }

    public IEnumerable<MarketDataItem<TMarketDataType>> ContinueReadUntil(long? timestamp = null)
    {
        while (_readerPosition < _itemsCount)
        {
            if (timestamp is not null && _next.Timestamp > timestamp)
                yield break;

            yield return _next;
            if (!ReadSingleItem(out _next))
                yield break;
        }
    }

    public void ResetReader()
    {
        if (_sourceFileStream?.CanSeek ?? false)
        {
            if (_compressionType is not CompressionType.NoCompression)
                _compressionStream?.Dispose();

            _sourceFileStream.Seek(sizeof(MarketDataCacheMeta), SeekOrigin.Begin);
            _compressionStream = _compressionType switch
            {
                CompressionType.NoCompression => _sourceFileStream,
                CompressionType.Brotli => new BrotliStream(_sourceFileStream, CompressionMode.Decompress, true),
                CompressionType.Deflate => new DeflateStream(_sourceFileStream, CompressionMode.Decompress, true),
                CompressionType.GZip => new GZipStream(_sourceFileStream, CompressionMode.Decompress, true),
                _ => _sourceFileStream
            };
            _readerPosition = 0;
            ReadSingleItem(out _next);
        }
        else
        {
            throw new InvalidOperationException("Source data file is not readable");
        }
    }

    private bool ReadSingleItem(out MarketDataItem<TMarketDataType> marketDataItem)
    {
        marketDataItem = default;
        if (_isEmpty)
            return false;
        if (_readerPosition >= _itemsCount)
            return false;
        try
        {
            var nextItemExists = ReadSingleItem(_compressionStream!, out marketDataItem);
            if (nextItemExists)
            {
                _readerPosition++;
            }
            return nextItemExists;
        }
        catch
        {
            return false;
        }
    }

    private MarketDataCacheMeta ExtractMeta()
    {
        _sourceFileStream!.Seek(0, SeekOrigin.Begin);
        if (!ReadSingleItem<MarketDataCacheMeta>(_sourceFileStream, out var meta))
            throw new InvalidOperationException(
                $"Invalid market data file. Expected {nameof(MarketDataCacheMeta)} header.");
        return meta;
    }

    private void WriteMeta(MarketDataCacheMeta meta)
    {
        _sourceFileStream!.Seek(0, SeekOrigin.Begin);
        WriteInternal(_sourceFileStream, meta);
    }

    private static void WriteInternal<TDataType>(Stream stream, TDataType data)
        where TDataType : unmanaged
    {
        var messageSize = sizeof(TDataType);
        stream.Write(new ReadOnlySpan<byte>((byte*)&data, messageSize));
    }

    private static bool ReadSingleItem<TDataType>(Stream stream, out TDataType data)
        where TDataType : unmanaged
    {
        var size = sizeof(TDataType);
        Span<byte> buffer = stackalloc byte[size];
        data = default;
        var bytesRead = stream.ReadSafe(buffer);
        if (bytesRead != size)
            return false;
        fixed (byte* ptr = buffer)
        {
            data = *(TDataType*)ptr;
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_isEmpty)
            return;
        if (_compressionType is not CompressionType.NoCompression)
            _compressionStream?.Dispose();

        if (!_isReader)
        {
            var meta = new MarketDataCacheMeta
            {
                Version = 1,
                Hash = _hash,
                CompressionType = _compressionType,
                CompressionLevel = _compressionLevel,
                ItemsCount = _itemsCount,
                BuildTime = DateTime.UtcNow
            };
            WriteMeta(meta);
            _sourceFileStream?.Dispose();
            _disposed = true;
            return;
        }

        _disposed = true;
        _sourceFileStream?.Dispose();
    }
}
