using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.MarketData;

// ReSharper disable StaticMemberInGenericType

namespace Banana.Backtest.Common.Services;

public unsafe class MarketDataCacheReaderMmf<TMarketDataType> : IMarketDataCacheReader<TMarketDataType>
    where TMarketDataType : unmanaged
{
    private static readonly FeedType Feed = typeof(TMarketDataType).GetCustomAttribute<FeedAttribute>()?.Feed
                                      ?? throw new ArgumentException(
                                          $"Feed type is not defined for {typeof(TMarketDataType).Name}. Ensure that {nameof(FeedAttribute)} is set.");
    private static readonly long ItemSize = sizeof(MarketDataItem<TMarketDataType>);
    private static readonly long MetaSize = sizeof(MarketDataCacheMeta);

    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _memoryMappedViewAccessor;
    private readonly byte* _readerPointer;

    private long _readerPosition;
    private MarketDataItem<TMarketDataType> _next;
    private long _readOffset;

    public MarketDataCacheReaderMmf(string? sourcesDirectory, MarketDataHash hash)
    {
        Hash = hash.For(Feed);
        var filePath = Hash.FilePath(sourcesDirectory);
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        _memoryMappedFile = MemoryMappedFile.CreateFromFile(
            fileStream,
            Hash.ToString(),
            0,
            MemoryMappedFileAccess.Read,
            HandleInheritability.Inheritable,
            false);

        _memoryMappedViewAccessor = _memoryMappedFile.CreateViewAccessor(
            0,
            0,
            MemoryMappedFileAccess.Read);

        _readerPointer = (byte*)_memoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();
        var meta = ExtractMeta();
        if (meta.CompressionType is not CompressionType.NoCompression)
            throw new NotSupportedException("It is only possible to read files with no compression using MMF.");
        ItemsCount = meta.ItemsCount;
        IsEmpty = ItemsCount == 0;
        ReadNextItem(out _next);
    }

    public bool IsEmpty { get; }
    public long ItemsCount { get; }
    public MarketDataHash Hash { get; }

    public IEnumerable<MarketDataItem<TMarketDataType>> ContinueReadUntil(long? timestamp = null)
    {
        while (_readerPosition < ItemsCount)
        {
            if (timestamp is not null && _next.Timestamp > timestamp)
                yield break;

            yield return _next;
            if (!ReadNextItem(out _next))
                yield break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadNextItem(out MarketDataItem<TMarketDataType> item)
    {
        item = default;
        if (IsEmpty)
            return false;
        if (_readerPosition >= ItemsCount)
            return false;
        if (!ReadUnsafe(_readerPointer, _readOffset, out item))
            return false;
        _readerPosition++;
        _readOffset += ItemSize;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MarketDataCacheMeta ExtractMeta()
    {
        if (ReadUnsafe<MarketDataCacheMeta>(_readerPointer, _readOffset, out var meta))
            _readOffset += MetaSize;
        return meta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ReadUnsafe<TItem>(byte* readerPointer, long offset, out TItem item)
        where TItem : unmanaged
    {
        item = *(TItem*)(readerPointer + offset);
        return true;
    }

    public void ResetReader()
    {
        _readerPosition = 0;
        _readOffset = MetaSize;
        ReadNextItem(out _next);
    }

    public void Dispose()
    {
        _memoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousRelease();
        _memoryMappedFile.Dispose();
        GC.SuppressFinalize(this);
    }
}
